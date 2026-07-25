import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ReservationsClient, ReservationDto, CreateReservationCommand, UpdateReservationCommand,
  ReservationStatus, CarsClient, CarDto, ClientsClient, ClientDto
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';

@Component({
  selector: 'app-reservation-form',
  templateUrl: './reservation-form.component.html',
  styleUrls: ['./reservation-form.component.css']
})
export class ReservationFormComponent implements OnInit {
  form: FormGroup;
  reservationId?: number;
  saving = false;
  errorMessage = '';

  cars: CarDto[] = [];
  clients: ClientDto[] = [];

  reservation?: ReservationDto;
  private rowVersion?: string;
  currency?: string;

  ReservationStatus = ReservationStatus;

  constructor(
    private fb: FormBuilder,
    private client: ReservationsClient,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      carId: [null, Validators.required],
      clientId: [null, Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      payedPrice: [null, Validators.min(0)],
      notes: ['']
    });
  }

  get isEdit(): boolean {
    return this.reservationId !== undefined;
  }

  get isPending(): boolean {
    return this.reservation?.status === ReservationStatus.Pending;
  }

  ngOnInit() {
    this.carsClient.getCars(1, 1000, null, null, null).subscribe({
      next: r => this.cars = r.items || [],
      error: err => console.error(err)
    });
    this.clientsClient.getClients(1, 1000, null, null).subscribe({
      next: r => this.clients = r.items || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.reservationId = +idParam;
      this.reload();
    }
  }

  private reload() {
    if (!this.reservationId) return;
    this.client.getReservationById(this.reservationId).subscribe({
      next: dto => {
        this.reservation = dto;
        this.rowVersion = dto.rowVersion;
        this.currency = dto.price?.currency;
        this.form.patchValue({
          carId: dto.carId ?? null,
          clientId: dto.clientId ?? null,
          startDate: toDateInput(dto.startDate),
          endDate: toDateInput(dto.endDate),
          payedPrice: dto.payedPrice?.amount ?? null,
          notes: dto.notes ?? ''
        });
        if (!this.isPending) {
          this.form.disable();
        }
      },
      error: err => console.error(err)
    });
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving = true;
    this.errorMessage = '';
    const v = this.form.getRawValue();

    if (this.isEdit) {
      const command = new UpdateReservationCommand({
        id: this.reservationId,
        rowVersion: this.rowVersion,
        carId: v.carId,
        clientId: v.clientId,
        startDate: fromDateInput(v.startDate),
        endDate: fromDateInput(v.endDate),
        payedPrice: v.payedPrice ?? undefined,
        notes: v.notes || undefined
      });
      this.client.updateReservation(this.reservationId!, command).subscribe({
        next: () => { this.saving = false; this.reload(); },
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateReservationCommand({
        carId: v.carId,
        clientId: v.clientId,
        startDate: fromDateInput(v.startDate),
        endDate: fromDateInput(v.endDate),
        payedPrice: v.payedPrice ?? undefined,
        notes: v.notes || undefined
      });
      this.client.createReservation(command).subscribe({
        next: () => this.router.navigate(['/reservation']),
        error: err => this.handleError(err)
      });
    }
  }

  confirm() {
    if (!this.reservationId) return;
    if (!confirm('Confirm this reservation into a renting?')) return;
    this.client.confirmReservation(this.reservationId).subscribe({
      next: rentingId => this.router.navigate(['/renting', rentingId]),
      error: err => this.handleError(err)
    });
  }

  cancel() {
    if (!this.reservationId) return;
    if (!confirm('Cancel this reservation?')) return;
    this.client.cancelReservation(this.reservationId).subscribe({
      next: () => this.router.navigate(['/reservation']),
      error: err => this.handleError(err)
    });
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage =
        'This reservation was reloaded by another user since you opened it. Reload the page to get the latest version, then re-apply your changes.';
      return;
    }

    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
