import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  RentingsClient, RentingDto, CreateRentingCommand, UpdateRentingCommand,
  ChangeRentingStateCommand, RentingState, RentingHistoryDto,
  CarsClient, CarDto, ClientsClient, ClientDto,
  ExtraServicesClient, ExtraServiceDto, CreateExtraServiceCommand,
  ExtraServiceTypesClient, ExtraServicesTypeDto,
  PaymentsClient, PaymentDto, CreatePaymentCommand, PaymentMethod
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-renting-form',
  templateUrl: './renting-form.component.html',
  styleUrls: ['./renting-form.component.css']
})
export class RentingFormComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  form: FormGroup;
  rentingId?: number;
  saving = false;
  errorMessage = '';

  cars: CarDto[] = [];
  clients: ClientDto[] = [];

  // Edit-mode state
  renting?: RentingDto;
  private rowVersion?: string;
  currency?: string;

  extraServices: ExtraServiceDto[] = [];
  extraServiceTypes: ExtraServicesTypeDto[] = [];
  newExtraTypeId: number | null = null;
  newExtraAmount: number | null = null;

  payments: PaymentDto[] = [];
  newPaymentAmount: number | null = null;
  newPaymentMethod: PaymentMethod = PaymentMethod.Cash;
  newPaymentNotes = '';

  history: RentingHistoryDto[] = [];

  // The extra-services and payments panels only apply when the agency has those
  // features; otherwise their (feature-gated) endpoints would 403.
  canUseExtraServices = false;
  canUsePayments = false;

  RentingState = RentingState;
  PaymentMethod = PaymentMethod;
  paymentMethods = [
    { value: PaymentMethod.Cash, labelKey: 'enums.paymentMethod.cash' },
    { value: PaymentMethod.Card, labelKey: 'enums.paymentMethod.card' },
    { value: PaymentMethod.Transfer, labelKey: 'enums.paymentMethod.transfer' },
    { value: PaymentMethod.Cheque, labelKey: 'enums.paymentMethod.cheque' }
  ];

  constructor(
    private fb: FormBuilder,
    private client: RentingsClient,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private extraServicesClient: ExtraServicesClient,
    private extraServiceTypesClient: ExtraServiceTypesClient,
    private paymentsClient: PaymentsClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      carId: [null, Validators.required],
      clientId: [null, Validators.required],
      secondClientId: [null],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      startMileage: [null, Validators.min(0)],
      endMileage: [null, Validators.min(0)],
      notes: ['']
    });
  }

  get isEdit(): boolean {
    return this.rentingId !== undefined;
  }

  get isTerminal(): boolean {
    return this.renting?.rentingState === RentingState.Done
      || this.renting?.rentingState === RentingState.Cancelled;
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

    // Resolve the agency's features before loading the feature-gated panels.
    this.auth.currentUser$.subscribe(user => {
      this.canUseExtraServices = AuthService.canAccessModule(user, 'ExtraServices', 'ExtraService.Read');
      this.canUsePayments = AuthService.canAccessModule(user, 'Payments', 'Payment.Read');

      const idParam = this.route.snapshot.paramMap.get('id');
      if (idParam) {
        this.rentingId = +idParam;
        this.reload();
        if (this.canUseExtraServices) {
          this.extraServiceTypesClient.getExtraServiceTypes(true).subscribe({
            next: types => this.extraServiceTypes = types || [],
            error: err => console.error(err)
          });
        }
      }
    });
  }

  private reload() {
    if (!this.rentingId) return;
    this.client.getRentingById(this.rentingId).subscribe({
      next: dto => {
        this.renting = dto;
        this.rowVersion = dto.rowVersion;
        this.currency = dto.price?.currency;
        this.form.patchValue({
          carId: dto.carId ?? null,
          clientId: dto.clientId ?? null,
          secondClientId: dto.secondClientId ?? null,
          startDate: toDateInput(dto.startDate),
          endDate: toDateInput(dto.endDate),
          startMileage: dto.startMileage ?? null,
          endMileage: dto.endMileage ?? null,
          notes: dto.notes ?? ''
        });
        if (this.isTerminal) {
          this.form.disable();
        }
      },
      error: err => console.error(err)
    });
    if (this.canUseExtraServices) {
      this.extraServicesClient.getExtraServicesByRenting(this.rentingId).subscribe({
        next: list => this.extraServices = list || [],
        error: err => console.error(err)
      });
    }
    if (this.canUsePayments) {
      this.paymentsClient.getPayments(1, 100, this.rentingId, null, null).subscribe({
        next: r => this.payments = r.items || [],
        error: err => console.error(err)
      });
    }
    this.client.getRentingHistory(this.rentingId).subscribe({
      next: list => this.history = list || [],
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
      const command = new UpdateRentingCommand({
        id: this.rentingId,
        rowVersion: this.rowVersion,
        carId: v.carId,
        clientId: v.clientId,
        secondClientId: v.secondClientId ?? undefined,
        startDate: fromDateInput(v.startDate),
        endDate: fromDateInput(v.endDate),
        startMileage: v.startMileage ?? undefined,
        endMileage: v.endMileage ?? undefined,
        notes: v.notes || undefined
      });
      this.client.updateRenting(this.rentingId!, command).subscribe({
        next: () => { this.saving = false; this.reload(); },
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateRentingCommand({
        carId: v.carId,
        clientId: v.clientId,
        secondClientId: v.secondClientId ?? undefined,
        startDate: fromDateInput(v.startDate),
        endDate: fromDateInput(v.endDate),
        startMileage: v.startMileage ?? undefined,
        notes: v.notes || undefined
      });
      this.client.createRenting(command).subscribe({
        next: id => this.router.navigate(['/renting', id]),
        error: err => this.handleError(err)
      });
    }
  }

  startRenting() {
    const value = prompt(this.transloco.translate('renting.promptPickupMileage'));
    if (value === null) return; // cancelled
    const mileage = value.trim() === '' ? undefined : Number(value);
    this.changeState(RentingState.InProgress, mileage);
  }

  completeRenting() {
    const value = prompt(this.transloco.translate('renting.promptReturnMileage'));
    if (value === null) return;
    const mileage = value.trim() === '' ? undefined : Number(value);
    this.changeState(RentingState.Done, mileage);
  }

  private changeState(newState: RentingState, mileage?: number) {
    if (!this.rentingId) return;
    this.errorMessage = '';
    const command = new ChangeRentingStateCommand({
      id: this.rentingId,
      rowVersion: this.rowVersion,
      newState,
      mileage
    });
    this.client.changeRentingState(this.rentingId, command).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  cancelRenting() {
    if (!this.rentingId) return;
    if (!confirm(this.transloco.translate('renting.confirmCancel'))) return;
    this.client.cancelRenting(this.rentingId).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  addExtraService() {
    if (!this.rentingId || !this.newExtraTypeId) return;
    const command = new CreateExtraServiceCommand({
      rentingId: this.rentingId,
      extraServicesTypeId: this.newExtraTypeId,
      amount: this.newExtraAmount ?? undefined
    });
    this.extraServicesClient.createExtraService(command).subscribe({
      next: () => {
        this.newExtraTypeId = null;
        this.newExtraAmount = null;
        this.reload();
      },
      error: err => this.handleError(err)
    });
  }

  deleteExtraService(item: ExtraServiceDto) {
    if (!item.id) return;
    this.extraServicesClient.deleteExtraService(item.id).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  addPayment() {
    if (!this.rentingId || !this.newPaymentAmount) return;
    const command = new CreatePaymentCommand({
      rentingId: this.rentingId,
      amount: this.newPaymentAmount,
      method: this.newPaymentMethod,
      notes: this.newPaymentNotes || undefined
    });
    this.paymentsClient.createPayment(command).subscribe({
      next: () => {
        this.newPaymentAmount = null;
        this.newPaymentNotes = '';
        this.reload();
      },
      error: err => this.handleError(err)
    });
  }

  reversePayment(item: PaymentDto) {
    if (!item.id) return;
    if (!confirm(this.transloco.translate('renting.confirmReverse'))) return;
    this.paymentsClient.reversePayment(item.id).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  // Returns a transloco key for the state chip; the raw enum name would show
  // "NotYet" untranslated.
  stateLabelKey(state?: RentingState): string {
    switch (state) {
      case RentingState.NotYet: return 'enums.rentingState.notYet';
      case RentingState.InProgress: return 'enums.rentingState.inProgress';
      case RentingState.Done: return 'enums.rentingState.done';
      case RentingState.Cancelled: return 'enums.rentingState.cancelled';
      default: return '';
    }
  }

  // Returns a transloco key; the template pipes it.
  methodLabelKey(method?: PaymentMethod): string {
    return this.paymentMethods.find(m => m.value === method)?.labelKey ?? '';
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage = this.transloco.translate('renting.concurrency');
      return;
    }

    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
