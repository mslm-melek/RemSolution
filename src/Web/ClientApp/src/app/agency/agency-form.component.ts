import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AgenciesClient, CountriesClient, CountryDto, AgencyDto,
  AgencyBranchInput, CreateAgencyBranchCommand, CreateAgencyCommand,
  UpdateAgencyBranchCommand, UpdateAgencyCommand
} from '../web-api-client';
import { extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { BranchDraft, BranchEdit } from '../shared/branches-editor.component';
import { PickedLocation } from '../shared/map-picker.component';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-agency-form',
  templateUrl: './agency-form.component.html',
  styleUrls: ['./agency-form.component.css']
})
export class AgencyFormComponent implements OnInit {
  // Error banners are plain strings, so they are translated imperatively.
  private readonly transloco = inject(TranslocoService);
  form: FormGroup;
  countries: CountryDto[] = [];
  agencyId?: number;
  saving = false;
  errorMessage = '';

  // The HQ pin, kept beside the form: the map picker owns it and the API validates it.
  latitude: number | null = null;
  longitude: number | null = null;

  // On a new agency these are held here and created with it; on an existing one
  // each change is saved at once through the branch sub-resource.
  branches: BranchDraft[] = [];
  branchesSaving = false;

  // Optimistic-concurrency token read with the agency and echoed back on update.
  private rowVersion?: string;

  // From the create response: the login provisioned for the agency, and its
  // one-time password only when the welcome mail did not go out.
  createdAdmin: { id: number; userName?: string; password?: string; emailSent: boolean } | null = null;

  constructor(
    private fb: FormBuilder,
    private client: AgenciesClient,
    private countriesClient: CountriesClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      email: ['', [Validators.email, Validators.maxLength(320)]],
      phoneNumber: ['', Validators.maxLength(50)],
      address: ['', Validators.maxLength(500)],
      countryId: [null, Validators.required],
      currency: ['TND', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
      cancellationWindowHours: [24, [Validators.required, Validators.min(0)]],
      reservationExpiryHours: [48, [Validators.required, Validators.min(1)]],
      // Left empty, the API falls back to the agency's own address.
      adminEmail: ['', [Validators.email, Validators.maxLength(320)]],
      adminFullName: ['', Validators.maxLength(200)]
    });
  }

  get isEdit(): boolean {
    return this.agencyId !== undefined;
  }

  ngOnInit() {
    this.countriesClient.getCountries().subscribe({
      next: countries => this.countries = countries || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.agencyId = +idParam;
      this.client.getAgencyById(this.agencyId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
      this.loadBranches();
    }
  }

  private populate(dto: AgencyDto) {
    this.form.patchValue({
      name: dto.name ?? '',
      email: dto.email ?? '',
      phoneNumber: dto.phoneNumber ?? '',
      address: dto.address ?? '',
      countryId: dto.countryId ?? null,
      currency: dto.currency ?? 'TND',
      cancellationWindowHours: dto.cancellationWindowHours ?? 24,
      reservationExpiryHours: dto.reservationExpiryHours ?? 48
    });

    this.latitude = dto.latitude ?? null;
    this.longitude = dto.longitude ?? null;
    this.rowVersion = dto.rowVersion;
  }

  private loadBranches() {
    if (!this.agencyId) return;

    this.client.getAgencyBranches(this.agencyId).subscribe({
      next: branches => this.branches = (branches || []).map(branch => ({
        id: branch.id,
        name: branch.name ?? '',
        countryId: branch.countryId ?? null,
        address: branch.address ?? null,
        latitude: branch.latitude ?? null,
        longitude: branch.longitude ?? null
      })),
      error: err => console.error(err)
    });
  }

  // The picker reports the address after the coordinates, so a null address means
  // "nothing to suggest yet" and what is typed is left alone.
  onPicked(picked: PickedLocation) {
    this.latitude = picked.latitude;
    this.longitude = picked.longitude;

    if (picked.address) {
      this.form.patchValue({ address: picked.address });
    }
  }

  onBranchAdded(draft: BranchDraft) {
    if (!this.isEdit) {
      // No agency to save against yet; held and created with it.
      this.branches = [...this.branches, draft];
      return;
    }

    this.branchesSaving = true;
    this.client.createAgencyBranch(this.agencyId!, new CreateAgencyBranchCommand({
      agencyId: this.agencyId!,
      ...this.toBranchPayload(draft)
    })).subscribe({
      next: () => this.afterBranchSave(),
      error: err => this.handleBranchError(err)
    });
  }

  onBranchUpdated(edit: BranchEdit) {
    if (!this.isEdit || edit.values.id === undefined) {
      this.branches = this.branches.map(branch =>
        branch === edit.target ? edit.values : branch);
      return;
    }

    this.branchesSaving = true;
    this.client.updateAgencyBranch(this.agencyId!, edit.values.id, new UpdateAgencyBranchCommand({
      agencyId: this.agencyId!,
      id: edit.values.id,
      ...this.toBranchPayload(edit.values)
    })).subscribe({
      next: () => this.afterBranchSave(),
      error: err => this.handleBranchError(err)
    });
  }

  onBranchRemoved(branch: BranchDraft) {
    if (!this.isEdit || branch.id === undefined) {
      this.branches = this.branches.filter(candidate => candidate !== branch);
      return;
    }

    this.branchesSaving = true;
    this.client.deleteAgencyBranch(this.agencyId!, branch.id).subscribe({
      next: () => this.afterBranchSave(),
      error: err => this.handleBranchError(err)
    });
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    const payload = this.toPayload();

    if (this.isEdit) {
      // Branches were saved as they were edited, so they are not part of this.
      const command = new UpdateAgencyCommand({ id: this.agencyId, rowVersion: this.rowVersion, ...payload });
      this.client.updateAgency(this.agencyId!, command).subscribe({
        next: () => this.router.navigate(['/agency']),
        error: err => this.handleError(err)
      });
    } else {
      const v = this.form.value;
      const command = new CreateAgencyCommand({
        ...payload,
        adminEmail: v.adminEmail || undefined,
        adminFullName: v.adminFullName || undefined,
        branches: this.branches.map(branch => new AgencyBranchInput(this.toBranchPayload(branch)))
      });
      this.client.createAgency(command).subscribe({
        // Stay here: the one-time password has to be seen, or its mail confirmed.
        next: result => {
          this.saving = false;
          this.createdAdmin = {
            id: result.id!,
            userName: result.adminUserName,
            // Only when the mail did not go out; otherwise it is already delivered.
            password: result.welcomeEmailSent ? undefined : result.adminTemporaryPassword,
            emailSent: !!result.welcomeEmailSent
          };
        },
        error: err => this.handleError(err)
      });
    }
  }

  // Leaves the hand-over panel for the agency's own page.
  continueToAgency() {
    if (this.createdAdmin) {
      this.router.navigate(['/agency', this.createdAdmin.id]);
    }
  }

  private toPayload() {
    const v = this.form.value;
    return {
      name: v.name,
      email: v.email || undefined,
      phoneNumber: v.phoneNumber || undefined,
      address: v.address || undefined,
      latitude: this.latitude ?? undefined,
      longitude: this.longitude ?? undefined,
      countryId: v.countryId,
      currency: (v.currency || '').toUpperCase(),
      cancellationWindowHours: v.cancellationWindowHours,
      reservationExpiryHours: v.reservationExpiryHours
    };
  }

  // undefined, not null, for the optional halves: the generated client omits
  // undefined properties, which is the shape the command declares.
  private toBranchPayload(branch: BranchDraft) {
    return {
      name: branch.name,
      countryId: branch.countryId ?? 0,
      address: branch.address || undefined,
      latitude: branch.latitude ?? undefined,
      longitude: branch.longitude ?? undefined
    };
  }

  private afterBranchSave() {
    this.branchesSaving = false;
    this.errorMessage = '';
    // Re-read: the list is ordered by name server-side.
    this.loadBranches();
  }

  private handleBranchError(err: any) {
    this.branchesSaving = false;
    this.handleError(err);
    // The list on screen may no longer match what is stored.
    this.loadBranches();
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage = this.transloco.translate('agency.concurrency');
      return;
    }

    const validationErrors = extractValidationErrors(err);
    if (validationErrors) {
      this.errorMessage = validationErrors;
    } else {
      this.errorMessage = this.transloco.translate('common.unexpectedError');
      console.error(err);
    }
  }
}
