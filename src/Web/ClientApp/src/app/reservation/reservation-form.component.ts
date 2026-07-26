import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ReservationsClient, ReservationDto, CreateReservationCommand, UpdateReservationCommand,
  RejectReservationCommand, ConvertReservationCommand, ReservationStatus,
  CarsClient, CarDto, ClientsClient, ClientDto,
  PaymentsClient, PaymentDto, CreatePaymentCommand, PaymentMethod, ClientBalanceDto
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

  // Payments panel (shown once the hold is confirmed/paid).
  payments: PaymentDto[] = [];
  balance?: ClientBalanceDto;
  newPaymentAmount: number | null = null;
  newPaymentMethod: PaymentMethod = PaymentMethod.Cash;
  newPaymentNotes = '';
  newPaymentIsRefund = false;

  ReservationStatus = ReservationStatus;
  PaymentMethod = PaymentMethod;
  paymentMethods = [
    { value: PaymentMethod.Cash, label: 'Cash' },
    { value: PaymentMethod.Card, label: 'Card' },
    { value: PaymentMethod.Transfer, label: 'Transfer' },
    { value: PaymentMethod.Cheque, label: 'Cheque' }
  ];

  constructor(
    private fb: FormBuilder,
    private client: ReservationsClient,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private paymentsClient: PaymentsClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      carId: [null, Validators.required],
      clientId: [null, Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      depositAmount: [null, Validators.min(0)],
      notes: ['']
    });
  }

  get isEdit(): boolean {
    return this.reservationId !== undefined;
  }

  get isPending(): boolean {
    return this.reservation?.status === ReservationStatus.PendingConfirmation;
  }

  get isConvertible(): boolean {
    return this.reservation?.status === ReservationStatus.Confirmed
        || this.reservation?.status === ReservationStatus.Paid;
  }

  get showPayments(): boolean {
    return this.isConvertible;
  }

  ngOnInit() {
    this.carsClient.getCars(1, 1000, null, null, null).subscribe({
      next: r => this.cars = r.items || [],
      error: err => console.error(err)
    });
    this.clientsClient.getClients(1, 1000, null, null).subscribe({
      next: r => this.clients = r.items || [],
      // A failed lookup leaves the picker empty, which is otherwise silent.
      error: err => { this.errorMessage = 'The client list could not be loaded.'; console.error(err); }
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
          depositAmount: dto.depositAmount?.amount ?? null,
          notes: dto.notes ?? ''
        });
        if (!this.isPending) {
          this.form.disable();
        } else {
          this.form.enable();
        }
        if (this.showPayments) {
          this.loadPayments();
        }
      },
      error: err => console.error(err)
    });
  }

  private loadPayments() {
    if (!this.reservationId) return;
    this.paymentsClient.getPayments(1, 100, null, null, this.reservationId).subscribe({
      next: r => this.payments = r.items || [],
      error: err => console.error(err)
    });
    if (this.reservation?.clientId) {
      this.paymentsClient.getClientBalance(this.reservation.clientId).subscribe({
        next: b => this.balance = b,
        error: err => console.error(err)
      });
    }
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
        depositAmount: v.depositAmount ?? undefined,
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
        depositAmount: v.depositAmount ?? undefined,
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
    this.client.confirmReservation(this.reservationId).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  reject() {
    if (!this.reservationId) return;
    const reason = prompt('Reason for rejecting this reservation (shown to the client):');
    if (!reason) return;
    this.client.rejectReservation(this.reservationId,
      new RejectReservationCommand({ id: this.reservationId, reason })).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  convert() {
    if (!this.reservationId) return;
    if (!confirm('Convert this reservation into a renting?')) return;
    const cin = prompt('Driver CIN (optional — used to match an existing client):') || undefined;
    const passeportNumber = cin ? undefined : (prompt('Driver passport number (optional):') || undefined);
    this.client.convertReservation(this.reservationId,
      new ConvertReservationCommand({ id: this.reservationId, cin, passeportNumber })).subscribe({
      next: rentingId => this.router.navigate(['/renting', rentingId]),
      error: err => this.handleError(err)
    });
  }

  cancel() {
    if (!this.reservationId) return;
    const reason = prompt('Reason for cancelling (optional):') ?? undefined;
    if (reason === undefined && !confirm('Cancel this reservation?')) return;
    this.client.cancelReservation(this.reservationId, reason).subscribe({
      next: () => this.router.navigate(['/reservation']),
      error: err => this.handleError(err)
    });
  }

  addPayment() {
    if (!this.reservationId || !this.newPaymentAmount) return;
    const command = new CreatePaymentCommand({
      reservationId: this.reservationId,
      amount: this.newPaymentAmount,
      isRefund: this.newPaymentIsRefund,
      method: this.newPaymentMethod,
      notes: this.newPaymentNotes || undefined
    });
    this.paymentsClient.createPayment(command).subscribe({
      next: () => {
        this.newPaymentAmount = null;
        this.newPaymentNotes = '';
        this.newPaymentIsRefund = false;
        this.reload();
      },
      error: err => this.handleError(err)
    });
  }

  reversePayment(item: PaymentDto) {
    if (!item.id) return;
    if (!confirm('Reverse this payment? An offsetting entry will be posted.')) return;
    this.paymentsClient.reversePayment(item.id).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  methodLabel(method?: PaymentMethod): string {
    return this.paymentMethods.find(m => m.value === method)?.label ?? '';
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
    setTimeout(() => this.errorMessage = '', 8000);
  }
}
