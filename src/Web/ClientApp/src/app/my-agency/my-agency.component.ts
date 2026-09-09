import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgenciesClient, AgencyDto, BranchesClient, CancellationFeeMode, CountriesClient, CountryDto,
  CreateBranchCommand, UpdateBranchCommand, UpdateMyAgencyCommand
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { BranchDraft, BranchEdit, BranchesEditorComponent } from '../shared/branches-editor.component';
import { MapPickerComponent, PickedLocation } from '../shared/map-picker.component';

/**
 * The agency administrator's view of their own agency: its details, its branches
 * and its staff. Branches go through the ordinary Branches endpoints, which take
 * the tenant from the caller's claim.
 */
@Component({
  selector: 'app-my-agency',
  templateUrl: './my-agency.component.html',
  styleUrls: ['./my-agency.component.css']
})
export class MyAgencyComponent implements OnInit {
  // Error banners are plain strings, so they are translated imperatively.
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  /**
   * Which tab opens. Only ever 0 or 1: /my-agency/reports lands on Reputation,
   * which the report notification links at, and everything else opens on
   * Details. A one-way binding, so the user can still move off it.
   */
  selectedTab = 0;

  @ViewChild(MapPickerComponent) private picker?: MapPickerComponent;
  @ViewChild(BranchesEditorComponent) private editor?: BranchesEditorComponent;

  form: FormGroup;
  countries: CountryDto[] = [];
  saving = false;
  errorMessage = '';

  CancellationFeeMode = CancellationFeeMode;

  /** The fee fields are meaningless while the agency charges nothing. */
  get chargesForCancellation(): boolean {
    return this.form.value.cancellationFeeMode !== CancellationFeeMode.None;
  }

  // The saved name, not the field's, so the head does not change as a rename is typed.
  agencyName = '';

  // Read-only: a currency change would reinterpret every stored amount, so it
  // stays with the platform administrator (see UpdateMyAgencyCommand).
  currency = '';

  latitude: number | null = null;
  longitude: number | null = null;

  branches: BranchDraft[] = [];
  branchesSaving = false;
  // Plan feature, so the tab is only offered when it is on.
  canBranches = false;

  // Feature only: these are agency settings, which an administrator holds by role.
  canNotifications = false;

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
      reservationExpiryHours: [48, [Validators.required, Validators.min(1)]],
      // Notification settings, saved by the same command as the rest. Bounds
      // mirror UpdateMyAgencyCommandValidator.
      expenseDueLeadDays: [14, [Validators.required, Validators.min(0), Validators.max(365)]],
      expenseDueLeadKilometers: [1000, [Validators.required, Validators.min(0), Validators.max(100000)]],
      reservationUpcomingLeadDays: [3, [Validators.required, Validators.min(0), Validators.max(365)]],
      notifyStaffByEmail: [true],
      notifyClientsByEmail: [false],
      clientReminderDaysBeforeStart: [2, [Validators.required, Validators.min(0), Validators.max(90)]],
      clientReminderDaysBeforeEnd: [1, [Validators.required, Validators.min(0), Validators.max(90)]],
      clientDocumentExpiryLeadDays: [30, [Validators.required, Validators.min(0), Validators.max(365)]],
      // Zero — the default — means no automatic purge. How long a passport copy
      // must be kept is the agency's jurisdiction's answer, not ours.
      personalDataRetentionMonths: [0, [Validators.required, Validators.min(0), Validators.max(120)]],
      // Tax. These three are what make a generated invoice a legal document;
      // each issued invoice keeps its own frozen copy, so changing them here
      // affects the next one and never the ones already out.
      taxIdentifier: ['', Validators.maxLength(40)],
      vatRatePercent: [19, [Validators.required, Validators.min(0), Validators.max(100)]],
      fiscalStampAmount: [1, [Validators.required, Validators.min(0)]],
      // Empty prints nothing. Informational only — the invoice stays denominated
      // in the agency's own currency (see AgencySettings).
      invoiceDisplayCurrency: ['', Validators.pattern(/^[A-Za-z]{3}$/)],
      // How long each document is valid where this agency trades. Used to fill
      // in an expiry the agent did not type; zero means it does not expire here.
      cinValidityYears: [10, [Validators.required, Validators.min(0), Validators.max(50)]],
      passeportValidityYears: [5, [Validators.required, Validators.min(0), Validators.max(50)]],
      drivingLicenceValidityYears: [10, [Validators.required, Validators.min(0), Validators.max(50)]],
      // What calling a booking off costs. Off by default; the free window has to
      // reach at least as far as the cutoff above, or nothing is ever charged
      // (the server refuses that combination too).
      cancellationFeeMode: [CancellationFeeMode.None],
      cancellationFeeValue: [0, [Validators.required, Validators.min(0)]],
      cancellationFreeHours: [48, [Validators.required, Validators.min(0), Validators.max(8760)]]
    });
  }

  ngOnInit() {
    // The reputation tab has a route of its own so a notification can point at
    // it; the rest of the screen is one page with tabs.
    this.selectedTab = this.route.snapshot.url.some(segment => segment.path === 'reports') ? 1 : 0;

    this.countriesClient.getCountries().subscribe({
      next: countries => this.countries = countries || [],
      error: err => console.error(err)
    });

    this.auth.currentUser$.subscribe(user => {
      this.canBranches = AuthService.canAccessModule(user, 'Branches', 'Branch.Read');
      this.canNotifications = user.features?.includes('Notifications') ?? false;

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
      reservationExpiryHours: dto.reservationExpiryHours ?? 48,
      // ?? not ||: zero is a real choice and must not fall back to the default.
      expenseDueLeadDays: dto.expenseDueLeadDays ?? 14,
      expenseDueLeadKilometers: dto.expenseDueLeadKilometers ?? 1000,
      reservationUpcomingLeadDays: dto.reservationUpcomingLeadDays ?? 3,
      notifyStaffByEmail: dto.notifyStaffByEmail ?? true,
      notifyClientsByEmail: dto.notifyClientsByEmail ?? false,
      clientReminderDaysBeforeStart: dto.clientReminderDaysBeforeStart ?? 2,
      clientReminderDaysBeforeEnd: dto.clientReminderDaysBeforeEnd ?? 1,
      clientDocumentExpiryLeadDays: dto.clientDocumentExpiryLeadDays ?? 30,
      personalDataRetentionMonths: dto.personalDataRetentionMonths ?? 0,
      taxIdentifier: dto.taxIdentifier ?? '',
      vatRatePercent: dto.vatRatePercent ?? 19,
      fiscalStampAmount: dto.fiscalStampAmount ?? 1,
      invoiceDisplayCurrency: dto.invoiceDisplayCurrency ?? '',
      cinValidityYears: dto.cinValidityYears ?? 10,
      passeportValidityYears: dto.passeportValidityYears ?? 5,
      drivingLicenceValidityYears: dto.drivingLicenceValidityYears ?? 10,
      cancellationFeeMode: dto.cancellationFeeMode ?? CancellationFeeMode.None,
      cancellationFeeValue: dto.cancellationFeeValue ?? 0,
      cancellationFreeHours: dto.cancellationFreeHours ?? 48
    });

    this.agencyName = dto.name ?? '';
    this.currency = dto.currency ?? '';
    this.latitude = dto.latitude ?? null;
    this.longitude = dto.longitude ?? null;
    this.rowVersion = dto.rowVersion;
  }

  /** The country pill in the head. Empty until the list and the agency are in. */
  get countryName(): string {
    const id = this.form.value.countryId;
    return this.countries.find(country => country.id === id)?.name ?? '';
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

  // The picker reports the address after the coordinates, so a null address means
  // "nothing to suggest yet" and what is typed is left alone.
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
      reservationExpiryHours: v.reservationExpiryHours,
      expenseDueLeadDays: v.expenseDueLeadDays,
      expenseDueLeadKilometers: v.expenseDueLeadKilometers,
      reservationUpcomingLeadDays: v.reservationUpcomingLeadDays,
      notifyStaffByEmail: v.notifyStaffByEmail,
      notifyClientsByEmail: v.notifyClientsByEmail,
      clientReminderDaysBeforeStart: v.clientReminderDaysBeforeStart,
      clientReminderDaysBeforeEnd: v.clientReminderDaysBeforeEnd,
      clientDocumentExpiryLeadDays: v.clientDocumentExpiryLeadDays,
      personalDataRetentionMonths: v.personalDataRetentionMonths,
      taxIdentifier: v.taxIdentifier || undefined,
      vatRatePercent: v.vatRatePercent,
      fiscalStampAmount: v.fiscalStampAmount,
      invoiceDisplayCurrency: (v.invoiceDisplayCurrency as string)?.toUpperCase() || undefined,
      cinValidityYears: v.cinValidityYears,
      passeportValidityYears: v.passeportValidityYears,
      drivingLicenceValidityYears: v.drivingLicenceValidityYears,
      cancellationFeeMode: v.cancellationFeeMode,
      cancellationFeeValue: v.cancellationFeeValue,
      cancellationFreeHours: v.cancellationFreeHours
    });

    this.client.updateMyAgency(command).subscribe({
      next: () => {
        this.saving = false;
        // Re-read for the new row version, so a second save is not stale.
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
    // Every row came from the server, so the guard is for the type only.
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

  // Leaflet measures on create, so a map built on a hidden tab comes out 0×0.
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
