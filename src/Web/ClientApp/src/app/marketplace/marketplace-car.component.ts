import { Component, OnInit, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MarketplaceClient, MarketplaceCarDto, CreateCustomerReservationCommand } from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { fromDateInput, toDateInput, extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-marketplace-car',
  templateUrl: './marketplace-car.component.html',
  styleUrls: ['./marketplace-car.component.css']
})
export class MarketplaceCarComponent implements OnInit {
  // The booking error banner is a plain string, so it is translated
  // imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);

  car?: MarketplaceCarDto;
  loading = true;
  notFound = false;

  isAuthenticated = false;
  isCustomer = false;

  form: FormGroup;
  booking = false;
  bookingError = '';

  constructor(
    private client: MarketplaceClient,
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthService,
    private fb: FormBuilder
  ) {
    const start = new Date();
    start.setDate(start.getDate() + 1);
    const end = new Date(start);
    end.setDate(end.getDate() + 3);
    this.form = this.fb.group({
      startDate: [toDateInput(start), Validators.required],
      endDate: [toDateInput(end), Validators.required],
      firstName: ['', [Validators.required, Validators.maxLength(200)]],
      lastName: ['', [Validators.required, Validators.maxLength(200)]],
      birthDate: ['', Validators.required]
    });
  }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.isCustomer = AuthService.isCustomer(user);
      // Pre-fill the driver name from the account's full name.
      if (this.isCustomer && user.fullName && !this.form.value.firstName) {
        const parts = user.fullName.trim().split(/\s+/);
        this.form.patchValue({ firstName: parts[0] ?? '', lastName: parts.slice(1).join(' ') });
      }
    });

    const id = +this.route.snapshot.paramMap.get('id')!;
    this.client.getCar(id).subscribe({
      next: car => { this.car = car; this.loading = false; },
      error: () => { this.notFound = true; this.loading = false; }
    });
  }

  signInToBook() {
    const returnUrl = encodeURIComponent(`/browse/car/${this.car?.id}`);
    window.location.href = `/Identity/Account/Register?returnUrl=${returnUrl}`;
  }

  book() {
    if (!this.car || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const start = fromDateInput(v.startDate);
    const end = fromDateInput(v.endDate);
    if (!start || !end || end <= start) {
      this.bookingError = this.transloco.translate('marketplace.invalidRange');
      return;
    }
    this.booking = true;
    this.bookingError = '';

    const command = new CreateCustomerReservationCommand({
      carId: this.car.id,
      startDate: start,
      endDate: end,
      firstName: v.firstName,
      lastName: v.lastName,
      birthDate: fromDateInput(v.birthDate)
    });

    this.client.bookCar(command).subscribe({
      next: () => this.router.navigate(['/my-reservations']),
      error: err => {
        this.booking = false;
        this.bookingError = extractValidationErrors(err) ?? 'Could not complete your booking. Please try again.';
      }
    });
  }
}
