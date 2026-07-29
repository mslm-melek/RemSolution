import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgenciesClient, AgencyDto, BranchesClient, CountriesClient, CountryDto,
  CreateBranchCommand, UpdateBranchCommand, UpdateMyAgencyCommand
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { BranchDraft, BranchEdit, BranchesEditorComponent } from '../shared/branches-editor.component';
import { MapPickerComponent, PickedLocation } from '../shared/map-picker.component';

/**
 * The agency administrator's own view of their agency: the details customers
 * see, the places they collect cars from, and the people who work there — the
 * three things an administrator manages about the agency itself, in one place.
 *
 * Branches are saved through the ordinary Branches endpoints, which take the
 * tenant from the caller's claim. That is the same data the platform
 * administrator edits from the agency form, by a different route.
 */
@Component({
  selector: 'app-my-agency',
  templateUrl: './my-agency.component.html',
  styleUrls: ['./my-agency.component.css']
})
export class MyAgencyComponent implements OnInit {
  // Error banners are plain strings, so they are translated imperatively rather
  // than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);

  @ViewChild(MapPickerComponent) private picker?: MapPickerComponent;
  @ViewChild(BranchesEditorComponent) private editor?: BranchesEditorComponent;

  form: FormGroup;
  countries: CountryDto[] = [];
  saving = false;
  errorMessage = '';

  // Shown but not editable: changing the currency would reinterpret every Money
  // amount the agency has already stored, so it stays with the platform
  // administrator (see UpdateMyAgencyCommand).
  currency = '';

  latitude: number | null = null;
  longitude: number | null = null;

  branches: BranchDraft[] = [];
  branchesSaving = false;
  // The Branches module is part of the agency's plan, so the tab is only offered
  // when it is on — the API would refuse the calls behind it otherwise.
  canBranches = false;

  private rowVersion?: string;

  constructor(
    private fb: FormBuilder,
    private client: AgenciesClient,
    private branchesClient: BranchesClient,
    private countriesClient: CountriesClient
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      email: ['', [Validators.email, Validators.maxLength(320)]],
      phoneNumber: ['', Validators.maxLength(50)],
      address: ['', Validators.maxLength(500)],
      countryId: [null, Validators.required],
      cancellationWindowHours: [24, [Validators.required, Validators.min(0)]],
      reservationExpiryHours: [48, [Validators.required, Validators.min(1)]]
    });
  }

  ngOnInit() {
    this.countriesClient.getCountries().subscribe({
      next: countries => this.countries = countries || [],
      error: err => console.error(err)
    });

    this.auth.currentUser$.subscribe(user => {
      this.canBranches = AuthService.canAccessModule(user, 'Branches', 'Branch.Read');

      if (this.canBranches) this.loadBranches();
    });

    this.load();
  }

  private load() {
    this.client.getMyAgency().subscribe({
      next: dto => this.populate(dto),
      error: err => console.error(err)
    });
  }

  private populate(dto: AgencyDto) {
    this.form.patchValue({
      name: dto.name ?? '',
      email: dto.email ?? '',
      phoneNumber: dto.phoneNumber ?? '',
      address: dto.address ?? '',
      countryId: dto.countryId ?? null,
      cancellationWindowHours: dto.cancellationWindowHours ?? 24,
      reservationExpiryHours: dto.reservationExpiryHours ?? 48
    });

    this.currency = dto.currency ?? '';
    this.latitude = dto.latitude ?? null;
    this.longitude = dto.longitude ?? null;
    this.rowVersion = dto.rowVersion;
  }

  private loadBranches() {
    this.branchesClient.getBranches().subscribe({
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

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.form.value;
    const command = new UpdateMyAgencyCommand({
      rowVersion: this.rowVersion,
      name: v.name,
      email: v.email || undefined,
      phoneNumber: v.phoneNumber || undefined,
      address: v.address || undefined,
      latitude: this.latitude ?? undefined,
      longitude: this.longitude ?? undefined,
      countryId: v.countryId,
      cancellationWindowHours: v.cancellationWindowHours,
      reservationExpiryHours: v.reservationExpiryHours
    });

    this.client.updateMyAgency(command).subscribe({
      next: () => {
        this.saving = false;
        // Re-read for the new row version, so a second save in the same visit
        // is not rejected as stale.
        this.load();
      },
      error: err => this.handleError(err)
    });
  }

  onBranchAdded(draft: BranchDraft) {
    this.branchesSaving = true;
    this.branchesClient.createBranch(new CreateBranchCommand(this.toBranchPayload(draft))).subscribe({
      next: () => this.afterBranchSave(),
      error: err => this.handleBranchError(err)
    });
  }

  onBranchUpdated(edit: BranchEdit) {
    // Every row here came from the server, so it has an id; the guard is for the
    // type, not for a case that can happen.
    if (edit.values.id === undefined) return;

    this.branchesSaving = true;
    this.branchesClient.updateBranch(edit.values.id, new UpdateBranchCommand({
      id: edit.values.id,
      ...this.toBranchPayload(edit.values)
    })).subscribe({
      next: () => this.afterBranchSave(),
      error: err => this.handleBranchError(err)
    });
  }

  onBranchRemoved(branch: BranchDraft) {
    if (branch.id === undefined) return;

    this.branchesSaving = true;
    this.branchesClient.deleteBranch(branch.id).subscribe({
      next: () => this.afterBranchSave(),
      error: err => this.handleBranchError(err)
    });
  }

  // Leaflet measures its container when the map is created, so a map built while
  // its tab was off screen comes out 0×0.
  onTabChange() {
    this.picker?.refresh();
    this.editor?.refresh();
  }

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
