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
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  form: FormGroup;
  countries: CountryDto[] = [];
  agencyId?: number;
  saving = false;
  errorMessage = '';

  // The HQ pin. Kept beside the form rather than in it: the map picker reads and
  // writes it, and nothing about it is validated in the browser (the API checks
  // the ranges and that the pair is complete).
  latitude: number | null = null;
  longitude: number | null = null;

  // The agency's locations. On a new agency these are held here and created with
  // it in one transaction; on an existing one each change is saved immediately
  // through the branch sub-resource, so what is on screen is what is stored.
  branches: BranchDraft[] = [];
  branchesSaving = false;

  // Optimistic-concurrency token read with the agency and echoed back on update.
  private rowVersion?: string;

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
      reservationExpiryHours: [48, [Validators.required, Validators.min(1)]]
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

  // The picker reports coordinates first and the reverse-geocoded address a
  // moment later, so an address of null means "nothing to suggest yet" — what is
  // already typed is left alone.
  onPicked(picked: PickedLocation) {
    this.latitude = picked.latitude;
    this.longitude = picked.longitude;

    if (picked.address) {
      this.form.patchValue({ address: picked.address });
    }
  }

  onBranchAdded(draft: BranchDraft) {
    if (!this.isEdit) {
      // Nothing to save against yet — the agency does not exist. Held until it
      // does, then created with it.
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
      const command = new CreateAgencyCommand({
        ...payload,
        branches: this.branches.map(branch => new AgencyBranchInput(this.toBranchPayload(branch)))
      });
      this.client.createAgency(command).subscribe({
        // Land on the agency management page so users/subscriptions can be set up.
        next: id => this.router.navigate(['/agency', id]),
        error: err => this.handleError(err)
      });
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

  // undefined rather than null for the optional halves: the generated client
  // omits undefined properties, and the API treats a missing coordinate as "no
  // pin" — a null would be sent and read the same way, but only one of the two
  // shapes is what the command declares.
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
    // Re-read rather than patch: the list is ordered by name server-side, and a
    // rename has to fall into its new place.
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
