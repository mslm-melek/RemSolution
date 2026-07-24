import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AgenciesClient, CountriesClient, CountryDto, AgencyDto,
  CreateAgencyCommand, UpdateAgencyCommand
} from '../web-api-client';
import { extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';

@Component({
  selector: 'app-agency-form',
  templateUrl: './agency-form.component.html',
  styleUrls: ['./agency-form.component.css']
})
export class AgencyFormComponent implements OnInit {
  form: FormGroup;
  countries: CountryDto[] = [];
  agencyId?: number;
  saving = false;
  errorMessage = '';

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

    this.rowVersion = dto.rowVersion;
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
      const command = new UpdateAgencyCommand({ id: this.agencyId, rowVersion: this.rowVersion, ...payload });
      this.client.updateAgency(this.agencyId!, command).subscribe({
        next: () => this.router.navigate(['/agency']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateAgencyCommand(payload);
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
      countryId: v.countryId,
      currency: (v.currency || '').toUpperCase(),
      cancellationWindowHours: v.cancellationWindowHours,
      reservationExpiryHours: v.reservationExpiryHours
    };
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage =
        'This agency was reloaded by another user since you opened it. Reload the page to get the latest version, then re-apply your changes.';
      return;
    }

    const validationErrors = extractValidationErrors(err);
    if (validationErrors) {
      this.errorMessage = validationErrors;
    } else {
      this.errorMessage = 'An unexpected error occurred. Please try again.';
      console.error(err);
    }
  }
}
