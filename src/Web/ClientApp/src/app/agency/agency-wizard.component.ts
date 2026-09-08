import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgenciesClient, AgencyBranchInput, AgencyFeatureDto, AgencyPublicationDto,
  AgencySubscriptionsClient, AssignAgencySubscriptionCommand, BranchDto,
  CountriesClient, CountryDto, CreateAgencyCarCommand, CreateAgencyCommand,
  ModelCarDto, ModelCarsClient, SetAgencyInvoiceSettingsCommand,
  SetAgencyPublicationCommand, SubscriptionPlanDto, SubscriptionPlansClient
} from '../web-api-client';
import { extractValidationErrors, extractProblemDetail, fromDateInput } from '../shared/form-utils';
import { BranchDraft } from '../shared/branches-editor.component';
import { PickedLocation } from '../shared/map-picker.component';

/** A car added during setup, kept so the step can show what it has done. */
interface AddedCar {
  matricule: string;
  modelName?: string;
  dailyRate?: number;
}

/**
 * Opening an agency, in the order the work actually happens: who they are, what
 * they are paying for, what their invoices must say, what they have to rent, and
 * only then the marketplace.
 *
 * The agency exists from the end of step one — everything after it writes to a
 * real row, and none of it is held in the browser waiting for a final save. That
 * is deliberate: an agency half set up is a normal state of the world (the
 * administrator can close the tab and come back through its page), and a wizard
 * that loses four steps of work on a refresh would be worse than four screens.
 *
 * Publication is the last step and nothing before it reaches a customer (see
 * Agency.PublishedAt).
 */
@Component({
  selector: 'app-agency-wizard',
  templateUrl: './agency-wizard.component.html',
  styleUrls: ['./agency-wizard.component.css']
})
export class AgencyWizardComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);

  /** Set once step one succeeds; every later step writes against it. */
  agencyId?: number;

  // The login provisioned with the agency, and its one-time password when the
  // welcome mail did not go out. Shown until the wizard is left.
  createdAdmin: { userName?: string; password?: string; emailSent: boolean } | null = null;

  countries: CountryDto[] = [];
  plans: SubscriptionPlanDto[] = [];
  models: ModelCarDto[] = [];
  branches: BranchDto[] = [];
  features: AgencyFeatureDto[] = [];
  publication?: AgencyPublicationDto;

  addedCars: AddedCar[] = [];

  detailsForm: FormGroup;
  planForm: FormGroup;
  invoiceForm: FormGroup;
  carForm: FormGroup;

  saving = false;
  errorMessage = '';

  // Held beside the form: the map picker owns the pair and the API validates it.
  latitude: number | null = null;
  longitude: number | null = null;

  // Created with the agency in one call, so they are drafts until then.
  branchDrafts: BranchDraft[] = [];

  constructor(
    private fb: FormBuilder,
    private client: AgenciesClient,
    private countriesClient: CountriesClient,
    private plansClient: SubscriptionPlansClient,
    private subscriptionsClient: AgencySubscriptionsClient,
    private modelsClient: ModelCarsClient,
    private router: Router
  ) {
    this.detailsForm = this.fb.group({
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

    this.planForm = this.fb.group({
      planId: [null, Validators.required],
      startDate: [today(), Validators.required],
      endDate: [inAYear(), Validators.required]
    });

    this.invoiceForm = this.fb.group({
      taxIdentifier: ['', Validators.maxLength(40)],
      vatRatePercent: [19, [Validators.required, Validators.min(0), Validators.max(100)]],
      fiscalStampAmount: [1, [Validators.required, Validators.min(0)]]
    });

    this.carForm = this.fb.group({
      matricule: ['', [Validators.required, Validators.maxLength(50)]],
      modelId: [null],
      branchId: [null],
      dailyRate: [null, Validators.min(0.01)]
    });
  }

  ngOnInit() {
    this.countriesClient.getCountries().subscribe({
      next: countries => this.countries = countries || [],
      error: err => console.error(err)
    });

    this.plansClient.getSubscriptionPlans().subscribe({
      next: plans => this.plans = plans || [],
      error: err => console.error(err)
    });

    // The whole catalogue: the fleet step picks from it, and it is small.
    this.modelsClient.getModelCars(1, 200, undefined, undefined, false).subscribe({
      next: page => this.models = page.items || [],
      error: err => console.error(err)
    });
  }

  // --- Step 1: the agency itself -------------------------------------------

  onPicked(picked: PickedLocation) {
    this.latitude = picked.latitude;
    this.longitude = picked.longitude;

    // The picker reports the address after the coordinates, so a null address
    // means "nothing to suggest yet" and what is typed is left alone.
    if (picked.address) {
      this.detailsForm.patchValue({ address: picked.address });
    }
  }

  onBranchAdded(draft: BranchDraft) {
    this.branchDrafts = [...this.branchDrafts, draft];
  }

  onBranchRemoved(branch: BranchDraft) {
    this.branchDrafts = this.branchDrafts.filter(candidate => candidate !== branch);
  }

  createAgency() {
    if (this.detailsForm.invalid || this.saving) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.detailsForm.value;

    this.client.createAgency(new CreateAgencyCommand({
      name: v.name,
      email: v.email || undefined,
      phoneNumber: v.phoneNumber || undefined,
      address: v.address || undefined,
      latitude: this.latitude ?? undefined,
      longitude: this.longitude ?? undefined,
      countryId: v.countryId,
      currency: (v.currency || '').toUpperCase(),
      cancellationWindowHours: v.cancellationWindowHours,
      reservationExpiryHours: v.reservationExpiryHours,
      adminEmail: v.adminEmail || undefined,
      adminFullName: v.adminFullName || undefined,
      branches: this.branchDrafts.map(branch => new AgencyBranchInput({
        name: branch.name,
        countryId: branch.countryId ?? 0,
        address: branch.address || undefined,
        latitude: branch.latitude ?? undefined,
        longitude: branch.longitude ?? undefined
      }))
    })).subscribe({
      next: result => {
        this.saving = false;
        this.agencyId = result.id;
        this.createdAdmin = {
          userName: result.adminUserName,
          // Only when the mail did not go out; otherwise it is already delivered.
          password: result.welcomeEmailSent ? undefined : result.adminTemporaryPassword,
          emailSent: !!result.welcomeEmailSent
        };
        // The steps after this one need what the agency now has.
        this.loadBranches();
        this.detailsForm.disable();
      },
      error: err => this.fail(err)
    });
  }

  // --- Step 2: what they are paying for ------------------------------------

  assignPlan() {
    if (!this.agencyId || this.planForm.invalid || this.saving) {
      this.planForm.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.planForm.value;

    this.subscriptionsClient.assignAgencySubscription(new AssignAgencySubscriptionCommand({
      agencyId: this.agencyId,
      planId: v.planId,
      startDate: fromDateInput(v.startDate),
      endDate: fromDateInput(v.endDate)
    })).subscribe({
      next: () => {
        this.saving = false;
        this.planForm.disable();
        // The modules come from the plan, so they can only be read once it is on.
        this.loadFeatures();
      },
      error: err => this.fail(err)
    });
  }

  get hasPlan(): boolean {
    return this.planForm.disabled;
  }

  private loadFeatures() {
    if (!this.agencyId) return;

    this.client.getAgencyFeatures(this.agencyId).subscribe({
      next: features => this.features = features || [],
      error: err => console.error(err)
    });
  }

  toggleFeature(feature: AgencyFeatureDto, enabled: boolean) {
    if (!this.agencyId) return;

    this.errorMessage = '';

    this.client.setAgencyFeature(this.agencyId, {
      agencyId: this.agencyId, feature: feature.feature, enabled
    } as any).subscribe({
      next: () => feature.enabled = enabled,
      error: err => this.fail(err)
    });
  }

  // --- Step 3: what the invoices must say ----------------------------------

  saveInvoiceSettings() {
    if (!this.agencyId || this.invoiceForm.invalid || this.saving) {
      this.invoiceForm.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.invoiceForm.value;

    this.client.setAgencyInvoiceSettings(this.agencyId, new SetAgencyInvoiceSettingsCommand({
      agencyId: this.agencyId,
      taxIdentifier: v.taxIdentifier || undefined,
      vatRatePercent: v.vatRatePercent,
      fiscalStampAmount: v.fiscalStampAmount
    })).subscribe({
      next: () => this.saving = false,
      error: err => this.fail(err)
    });
  }

  // --- Step 4: something to rent -------------------------------------------

  private loadBranches() {
    if (!this.agencyId) return;

    this.client.getAgencyBranches(this.agencyId).subscribe({
      next: branches => this.branches = branches || [],
      error: err => console.error(err)
    });
  }

  addCar() {
    if (!this.agencyId || this.carForm.invalid || this.saving) {
      this.carForm.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.carForm.value;

    this.client.createAgencyCar(this.agencyId, new CreateAgencyCarCommand({
      agencyId: this.agencyId,
      matricule: v.matricule,
      modelId: v.modelId ?? undefined,
      branchId: v.branchId ?? undefined,
      dailyRate: v.dailyRate ?? undefined
    })).subscribe({
      next: () => {
        this.saving = false;
        this.addedCars = [...this.addedCars, {
          matricule: v.matricule,
          modelName: this.models.find(m => m.id === v.modelId)?.name,
          dailyRate: v.dailyRate ?? undefined
        }];
        // Keep the branch: a fleet arrives at one place at a time.
        this.carForm.reset({ branchId: v.branchId });
      },
      error: err => this.fail(err)
    });
  }

  // --- Step 5: the marketplace ---------------------------------------------

  loadPublication() {
    if (!this.agencyId) return;

    this.client.getAgencyPublication(this.agencyId).subscribe({
      next: publication => this.publication = publication,
      error: err => console.error(err)
    });
  }

  publish() {
    if (!this.agencyId || this.saving) return;

    this.saving = true;
    this.errorMessage = '';

    this.client.setAgencyPublication(this.agencyId,
      new SetAgencyPublicationCommand({ id: this.agencyId, published: true })).subscribe({
      next: () => {
        this.saving = false;
        this.loadPublication();
      },
      error: err => this.fail(err)
    });
  }

  /** Leaves the wizard for the agency's own page, where everything is editable. */
  finish() {
    if (this.agencyId) {
      this.router.navigate(['/agency', this.agencyId]);
    }
  }

  private fail(err: any) {
    this.saving = false;
    this.errorMessage = extractValidationErrors(err)
      ?? extractProblemDetail(err)
      ?? this.transloco.translate('common.unexpectedError');
  }
}

// DateFieldComponent's value is a `yyyy-MM-dd` string, not a Date.
function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function inAYear(): string {
  const date = new Date();
  date.setFullYear(date.getFullYear() + 1);
  return date.toISOString().slice(0, 10);
}
