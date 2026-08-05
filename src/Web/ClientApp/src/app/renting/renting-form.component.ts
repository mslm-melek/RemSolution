import { Component, OnInit, inject } from '@angular/core';
import {
  AbstractControl, FormBuilder, FormControl, FormGroup, ValidationErrors, Validators
} from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, switchMap } from 'rxjs/operators';
import {
  RentingsClient, RentingDto, CreateRentingCommand, UpdateRentingCommand,
  ChangeRentingStateCommand, ChangeRentingEndDateCommand, RentingState, RentingHistoryDto,
  RentingQuoteDto, CarsClient, CarDto, CarStatus, ClientsClient, ClientDto, NewRentingClient,
  UpdateClientCommand, ClientDocumentType, FileParameter,
  ExtraServicesClient, ExtraServiceDto, CreateExtraServiceCommand,
  ExtraServiceTypesClient, ExtraServicesTypeDto,
  PaymentsClient, PaymentDto, CreatePaymentCommand, PaymentMethod,
  ContractsClient, ContractDto, GenerateContractCommand,
  FacturesClient, FactureDto, GenerateFactureCommand,
  DocumentTemplatesClient, DocumentTemplateDto, DocumentTemplateFieldDto, DocumentTemplateKind
} from '../web-api-client';
import {
  toDateInput, toDateTimeInput, fromDateInput, extractValidationErrors, isConcurrencyConflict
} from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { ReturnDialogComponent } from '../shared/return-dialog.component';
import { CancelDialogComponent } from '../shared/cancel-dialog.component';
import { TranslocoService } from '@jsverse/transloco';

// One of the selected client's identity papers, as shown in the renting form.
interface ClientDocumentSlot {
  type: ClientDocumentType;
  labelKey: string;
  url?: string;
  uploading: boolean;
}

// Whole days between two date-field values (yyyy-MM-ddTHH:mm here — a period is
// booked with the hour); null while either is unset. A started day is a billed
// day, matching PricingService — so the wizard can put a day count and a total
// on screen before the server has answered.
function daysBetween(start: string, end: string): number | null {
  if (!start || !end) return null;

  const from = new Date(start).getTime();
  const to = new Date(end).getTime();
  if (isNaN(from) || isNaN(to)) return null;

  const days = Math.ceil((to - from) / 86_400_000);
  return days > 0 ? days : null;
}

// The period has to be a period. Reported on the group rather than on endDate so
// the message survives whichever of the two dates was typed last.
function positivePeriod(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value;
  const end = group.get('endDate')?.value;

  if (!start || !end) return null;

  return daysBetween(start, end) === null ? { endBeforeStart: true } : null;
}

@Component({
  selector: 'app-renting-form',
  templateUrl: './renting-form.component.html',
  styleUrls: ['./renting-form.component.css']
})
export class RentingFormComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);

  // Creating is a wizard (vehicle → client → paperwork), editing is a detail
  // screen: the same three groups, but reachable in any order because the
  // booking already exists. Hence one form, split into the groups the stepper
  // needs for per-step validation.
  form: FormGroup;
  rentingId?: number;
  saving = false;
  errorMessage = '';

  cars: CarDto[] = [];
  clients: ClientDto[] = [];
  // Typed into the pickers to narrow them: an agency with hundreds of cars or
  // clients cannot scroll a plain dropdown.
  carFilter = '';
  clientFilter = '';
  secondClientFilter = '';

  // Edit-mode state
  renting?: RentingDto;
  private rowVersion?: string;
  currency?: string;

  // What the period would cost, priced by the server (see GetRentingQuoteQuery).
  // Re-asked whenever the car or a date changes, so the figure on screen is the
  // one that will be stored — unless the agent overrides it.
  quote?: RentingQuoteDto;
  loadingQuote = false;
  private readonly quoteTrigger = new Subject<void>();
  // A user who may create but not read rentings cannot be quoted; the price is
  // then typed rather than proposed.
  canQuote = false;

  extraServices: ExtraServiceDto[] = [];
  extraServiceTypes: ExtraServicesTypeDto[] = [];
  newExtraTypeId: number | null = null;
  newExtraAmount: number | null = null;

  payments: PaymentDto[] = [];
  newPaymentAmount: number | null = null;
  newPaymentMethod: PaymentMethod = PaymentMethod.Cash;
  newPaymentNotes = '';
  // The whole panel already requires Payment.Read, so the proof column always
  // has something to say: the file, a dash, or the button that attaches it.
  paymentColumns: string[] = ['date', 'amount', 'method', 'proof', 'actions'];
  canAttachProof = false;
  // Payment id whose proof is uploading, so only its own button waits.
  uploadingProofFor: number | null = null;

  history: RentingHistoryDto[] = [];

  contracts: ContractDto[] = [];
  factures: FactureDto[] = [];
  generatingContract = false;
  generatingFacture = false;

  // Which layout each document uses. Null means the agency's default (and then the
  // platform's shipped example), so an agency with no templates never sees a
  // decision it does not have.
  contractTemplates: DocumentTemplateDto[] = [];
  factureTemplates: DocumentTemplateDto[] = [];
  contractTemplateId: number | null = null;
  factureTemplateId: number | null = null;

  // The template's ask-each-time placeholders, and what the agent typed for them.
  // Keyed by placeholder name, shared across both documents (see
  // CreateRentingCommand.DocumentValues).
  contractPrompts: DocumentTemplateFieldDto[] = [];
  facturePrompts: DocumentTemplateFieldDto[] = [];
  documentValues: { [placeholder: string]: string } = {};

  DocumentTemplateKind = DocumentTemplateKind;

  // --- Change end date -----------------------------------------------------
  // The client wants the car for longer, or brings it back early. Kept out of
  // the main edit form on purpose: the form re-quotes the whole period when a
  // date moves, whereas this prices only the difference and lets the agent say
  // what should happen to the contract (see ChangeRentingEndDateCommand).
  endDatePanelOpen = false;
  newEndDate = '';
  reissueContract = true;
  changingEndDate = false;
  // The amended total, when the difference was agreed rather than calculated.
  overrideEndDatePrice = false;
  endDatePrice: number | null = null;

  // The picked client's own record, viewable and editable without leaving the
  // booking screen. Kept as a form of its own rather than a group inside `form`
  // so its validators can never block saving the RENTING — the two are separate
  // saves against separate endpoints.
  clientForm: FormGroup;
  selectedClient?: ClientDto;
  clientPanelOpen = false;
  loadingClient = false;
  savingClient = false;
  clientSavedMessage = '';
  clientDocuments: ClientDocumentSlot[] = [
    { type: ClientDocumentType.CIN, labelKey: 'client.cin', uploading: false },
    { type: ClientDocumentType.DrivingLicence, labelKey: 'client.drivingLicence', uploading: false },
    { type: ClientDocumentType.Passeport, labelKey: 'client.passeport', uploading: false }
  ];

  // The extra-services and payments panels only apply when the agency has those
  // features; otherwise their (feature-gated) endpoints would 403.
  canUseExtraServices = false;
  canUsePayments = false;
  // Same idea for paperwork, but split read from write: an agent who may read
  // contracts but not issue them still gets the list and the download links.
  canReadContracts = false;
  canGenerateContracts = false;
  canReadFactures = false;
  canGenerateFactures = false;
  // Reading the picked client's file and editing it are separate rights, so the
  // panel can be visible and read-only.
  canReadClients = false;
  canUpdateClients = false;

  RentingState = RentingState;
  CarStatus = CarStatus;
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
    private contractsClient: ContractsClient,
    private facturesClient: FacturesClient,
    private templatesClient: DocumentTemplatesClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      // Step 1 — what is being rented, for how long, and for how much.
      vehicle: this.fb.group({
        carId: [null, Validators.required],
        startDate: ['', Validators.required],
        endDate: ['', Validators.required],
        startMileage: [null, Validators.min(0)],
        endMileage: [null, Validators.min(0)],
        // Off by default: the quote is the price unless somebody says otherwise.
        overridePrice: [false],
        manualPrice: [null, Validators.min(0)]
      }, { validators: positivePeriod }),

      // Step 2 — who is renting, and who else will drive.
      client: this.fb.group({
        // 'existing' picks from the client list; 'new' fills newClient below and
        // the API creates the client in the same transaction as the renting.
        clientMode: ['existing'],
        clientId: [null, Validators.required],
        newClient: this.fb.group({
          firstName: ['', [Validators.required, Validators.maxLength(100)]],
          lastName: ['', [Validators.required, Validators.maxLength(100)]],
          // Filling this in gets the customer a portal login emailed to them
          // when the booking is saved.
          email: ['', [Validators.email, Validators.maxLength(256)]],
          birthDate: ['', Validators.required],
          cin: [''],
          drivingLicenceNumber: [''],
          passeportNumber: [''],
          description: ['']
        }),

        // The second driver has three states, not two: most bookings have none,
        // and the person who does drive is as likely to be a walk-in as the
        // renter (a couple at the counter). 'none' | 'existing' | 'new'.
        secondMode: ['none'],
        secondClientId: [null],
        secondNewClient: this.fb.group({
          firstName: ['', [Validators.required, Validators.maxLength(100)]],
          lastName: ['', [Validators.required, Validators.maxLength(100)]],
          birthDate: ['', Validators.required],
          cin: [''],
          drivingLicenceNumber: [''],
          passeportNumber: [''],
          description: ['']
        })
      }),

      // Step 3 — the paperwork that leaves with the client.
      paperwork: this.fb.group({
        notes: [''],
        generateContract: [false],
        generateFacture: [false]
      })
    });

    this.clientForm = this.fb.group({
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      birthDate: ['', Validators.required],
      birthPlace: [''],
      cin: [''],
      drivingLicenceNumber: [''],
      passeportNumber: [''],
      description: ['']
    });

    // Only the active client source is validated, so the untouched half never
    // blocks the save.
    this.applyClientMode('existing');
    this.clientGroup.get('clientMode')!.valueChanges.subscribe(mode => this.applyClientMode(mode));

    this.applySecondMode('none');
    this.clientGroup.get('secondMode')!.valueChanges.subscribe(mode => this.applySecondMode(mode));

    // Picking a different client swaps the panel's contents.
    this.clientGroup.get('clientId')!.valueChanges.subscribe(id => this.loadSelectedClient(id));

    // A price is either quoted or typed; the input only exists in the second case.
    this.vehicleGroup.get('overridePrice')!.valueChanges
      .subscribe(on => this.applyPriceMode(!!on));

    // Anything that changes what the period costs re-asks for the quote.
    for (const name of ['carId', 'startDate', 'endDate']) {
      this.vehicleGroup.get(name)!.valueChanges.subscribe(() => this.quoteTrigger.next());
    }

    // A different car has a different odometer, so the pickup reading follows the
    // choice (see offerCarMileage).
    this.vehicleGroup.get('carId')!.valueChanges.subscribe(() => this.offerCarMileage());

    // Debounced because the date inputs fire per keystroke; switchMap so a
    // slow answer for an old period can never overwrite a newer one.
    this.quoteTrigger
      .pipe(
        debounceTime(300),
        switchMap(() => {
          const v = this.vehicleGroup.getRawValue();
          const start = fromDateInput(v.startDate);
          const end = fromDateInput(v.endDate);

          if (!this.canQuote || !v.carId || !start || !end || !daysBetween(v.startDate, v.endDate)) {
            this.loadingQuote = false;
            return of(undefined);
          }

          this.loadingQuote = true;

          return this.client
            .getRentingQuote(v.carId, start, end, this.rentingId ?? null)
            // A quote is decoration, never a blocker: if it fails the agent can
            // still book, and the server prices the booking for itself anyway.
            .pipe(catchError(() => of(undefined)));
        })
      )
      .subscribe(quote => {
        this.loadingQuote = false;
        this.quote = quote;

        // Keeps the currency suffix on the manual-price input right even for a
        // car whose rate was never set (the quote carries the agency's).
        if (quote?.currency) this.currency = quote.currency;
      });
  }

  get vehicleGroup(): FormGroup {
    return this.form.get('vehicle') as FormGroup;
  }

  // The price controls are reached individually rather than through a container:
  // the price block is one shared <ng-template> rendered inside two different
  // form contexts (the wizard's step and the edit form's tab), and binding the
  // same group twice is what that would take.
  get overridePriceControl(): FormControl {
    return this.vehicleGroup.get('overridePrice') as FormControl;
  }

  get manualPriceControl(): FormControl {
    return this.vehicleGroup.get('manualPrice') as FormControl;
  }

  // Same reason as the price controls: the second-driver block is one shared
  // <ng-template> rendered in the wizard's step and in the edit form's tab.
  get secondModeControl(): FormControl {
    return this.clientGroup.get('secondMode') as FormControl;
  }

  get secondClientIdControl(): FormControl {
    return this.clientGroup.get('secondClientId') as FormControl;
  }

  get secondNewClientGroup(): FormGroup {
    return this.clientGroup.get('secondNewClient') as FormGroup;
  }

  get clientGroup(): FormGroup {
    return this.form.get('client') as FormGroup;
  }

  get paperworkGroup(): FormGroup {
    return this.form.get('paperwork') as FormGroup;
  }

  get isEdit(): boolean {
    return this.rentingId !== undefined;
  }

  get isTerminal(): boolean {
    return this.renting?.rentingState === RentingState.Done
      || this.renting?.rentingState === RentingState.Cancelled;
  }

  get isNewClient(): boolean {
    return this.clientGroup.get('clientMode')?.value === 'new';
  }

  get secondMode(): string {
    return this.clientGroup.get('secondMode')?.value ?? 'none';
  }

  get isNewSecondClient(): boolean {
    return this.secondMode === 'new';
  }

  get overridingPrice(): boolean {
    return !!this.vehicleGroup.get('overridePrice')?.value;
  }

  // --- What the booking costs ----------------------------------------------

  /** Billed days for the period on screen, from the quote or worked out here. */
  get billedDays(): number | null {
    if (this.quote?.billedDays) return this.quote.billedDays;

    const v = this.vehicleGroup.getRawValue();
    return daysBetween(v.startDate, v.endDate);
  }

  /** The car's rate, when it has one — the "× N days" part of the quote. */
  get dailyRateAmount(): number | null {
    return this.quote?.dailyRate?.amount ?? this.selectedCar?.dailyRate?.amount ?? null;
  }

  /** The automatic price for the period; null when the car has no rate. */
  get quotedAmount(): number | null {
    return this.quote?.price?.amount ?? null;
  }

  /**
   * The figure that will actually be stored — which is what the screen shows as
   * the agreed price. Three cases, in this order:
   *   overriding      → what was typed;
   *   editing, period unchanged → the snapshot already on the renting, because
   *                     saving would not touch it (that is the P.3 rule; showing
   *                     the quote here would claim a price that is not the
   *                     booking's);
   *   otherwise       → the quote.
   */
  get effectiveAmount(): number | null {
    if (this.overridingPrice) {
      const typed = this.vehicleGroup.get('manualPrice')?.value;
      return typed === null || typed === '' ? null : Number(typed);
    }

    if (this.isEdit && !this.willReprice) {
      return this.renting?.price?.amount ?? this.quotedAmount;
    }

    if (this.quotedAmount !== null) return this.quotedAmount;

    return this.isEdit ? this.renting?.price?.amount ?? null : null;
  }

  /**
   * True when the agreed price is not the calculated one and nothing on screen
   * would change it — i.e. this booking was negotiated. Said out loud so the gap
   * between the two figures reads as a decision rather than as a stale number.
   */
  get showsNegotiatedPrice(): boolean {
    if (this.overridingPrice || this.quotedAmount === null) return false;

    const effective = this.effectiveAmount;
    return effective !== null && Math.abs(effective - this.quotedAmount) >= 0.005;
  }

  get priceCurrency(): string {
    return this.quote?.currency
      ?? this.renting?.price?.currency
      ?? this.selectedCar?.dailyRate?.currency
      ?? this.currency
      ?? '';
  }

  /** Difference between the agreed price and the quote, for the agent to see. */
  get priceDelta(): number | null {
    if (!this.overridingPrice || this.quotedAmount === null) return null;

    const typed = this.effectiveAmount;
    if (typed === null) return null;

    const delta = typed - this.quotedAmount;
    return Math.abs(delta) < 0.005 ? null : delta;
  }

  /** True once the edit form's dates/car would re-quote the stored price. */
  get willReprice(): boolean {
    if (!this.isEdit || !this.renting || this.overridingPrice) return false;

    const v = this.vehicleGroup.getRawValue();
    return v.carId !== (this.renting.carId ?? null)
      || v.startDate !== toDateTimeInput(this.renting.startDate)
      || v.endDate !== toDateTimeInput(this.renting.endDate);
  }

  /** The quote says the car is taken for this period; the save would be a 409. */
  get periodConflicts(): boolean {
    return this.quote?.isAvailable === false && !!this.billedDays;
  }

  get carNotBookable(): boolean {
    return this.quote?.isCarBookable === false;
  }

  // --- Pickers -------------------------------------------------------------

  get selectedCar(): CarDto | undefined {
    const carId = this.vehicleGroup.get('carId')?.value;
    return this.cars.find(c => c.id === carId);
  }

  get pickedClient(): ClientDto | undefined {
    const clientId = this.clientGroup.get('clientId')?.value;
    return this.clients.find(c => c.id === clientId);
  }

  /**
   * The period on screen as dates, so the review step can format them per locale.
   *
   * Built with fromDateInput — the same helper that sends these two values — so
   * they are wall-clock stamped UTC like every other date in the app and the
   * template reads them back with `date:'…':'UTC'`. This used to be a bare
   * `new Date(value)`, which parses "2026-08-12T09:00" as LOCAL and so had to be
   * rendered without the timezone argument. That printed the right digits, but by
   * a second convention: the review showed a local-parsed date while the server
   * was sent a UTC-stamped one, and any later edit near either end of the string
   * (a seconds field, a trailing Z) would have silently split them apart.
   */
  get periodStart(): Date | undefined {
    return this.asDate(this.vehicleGroup.get('startDate')?.value);
  }

  get periodEnd(): Date | undefined {
    return this.asDate(this.vehicleGroup.get('endDate')?.value);
  }

  private asDate(value: string): Date | undefined {
    return fromDateInput(value);
  }

  carLabel(car?: CarDto): string {
    if (!car) return '';
    return `${car.modelName ?? ''} — ${car.matricule ?? ''}`;
  }

  clientLabel(client?: ClientDto): string {
    if (!client) return '';
    return `${client.lastName ?? ''} ${client.firstName ?? ''}`.trim();
  }

  get filteredCars(): CarDto[] {
    return this.matching(this.cars, this.carFilter, c => `${c.modelName} ${c.matricule}`);
  }

  get filteredClients(): ClientDto[] {
    return this.matching(this.clients, this.clientFilter, c => `${c.firstName} ${c.lastName} ${c.cin}`);
  }

  // The renter is left out: they are the one person the API is certain to refuse
  // as second driver, so offering them can only produce an error.
  get filteredSecondClients(): ClientDto[] {
    const renterId = this.clientGroup.get('clientId')?.value;

    return this.matching(
      this.clients.filter(c => c.id !== renterId),
      this.secondClientFilter,
      c => `${c.firstName} ${c.lastName} ${c.cin}`);
  }

  // Accent-insensitive enough for a picker: the data is stored under a CI/AI
  // collation, so a search here only has to match what the agent sees.
  private matching<T>(items: T[], filter: string, text: (item: T) => string): T[] {
    const needle = filter.trim().toLowerCase();
    if (!needle) return items;

    return items.filter(item => text(item).toLowerCase().includes(needle));
  }

  /** The picked client, for the review step. */
  get reviewClientName(): string {
    if (this.isNewClient) {
      const v = this.clientGroup.get('newClient')!.getRawValue();
      return `${v.lastName ?? ''} ${v.firstName ?? ''}`.trim();
    }

    return this.clientLabel(this.clients.find(c => c.id === this.clientGroup.get('clientId')?.value));
  }

  get reviewSecondClientName(): string {
    if (this.isNewSecondClient) {
      const v = this.clientGroup.get('secondNewClient')!.getRawValue();
      return `${v.lastName ?? ''} ${v.firstName ?? ''}`.trim();
    }

    return this.clientLabel(this.clients.find(c => c.id === this.clientGroup.get('secondClientId')?.value));
  }

  get pickedSecondClient(): ClientDto | undefined {
    const id = this.clientGroup.get('secondClientId')?.value;
    return this.clients.find(c => c.id === id);
  }

  /**
   * What the closed picker shows. The list is fetched a page at a time, so a
   * client outside it (or one since deactivated) would otherwise leave the field
   * looking empty while holding a value — the renting already carries the name,
   * so use that rather than show nothing.
   */
  get renterTriggerLabel(): string {
    return this.clientLabel(this.pickedClient)
      || (this.clientGroup.get('clientId')?.value === this.renting?.clientId
        ? this.renting?.clientName ?? ''
        : '');
  }

  get secondDriverTriggerLabel(): string {
    return this.clientLabel(this.pickedSecondClient)
      || (this.clientGroup.get('secondClientId')?.value === this.renting?.secondClientId
        ? this.renting?.secondClientName ?? ''
        : '');
  }

  ngOnInit() {
    this.clientsClient.getClients(1, 1000, null, null, null, null, null, null, false).subscribe({
      next: r => this.clients = r.items || [],
      error: err => console.error(err)
    });

    // Resolve the agency's features before loading the feature-gated panels.
    this.auth.currentUser$.subscribe(user => {
      this.canQuote = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canUseExtraServices = AuthService.canAccessModule(user, 'ExtraServices', 'ExtraService.Read');
      this.canUsePayments = AuthService.canAccessModule(user, 'Payments', 'Payment.Read');
      this.canReadContracts = AuthService.canAccessModule(user, 'Contracts', 'Contract.Read');
      this.canGenerateContracts = AuthService.canAccessModule(user, 'Contracts', 'Contract.Generate');
      this.canReadFactures = AuthService.canAccessModule(user, 'Factures', 'Facture.Read');
      this.canGenerateFactures = AuthService.canAccessModule(user, 'Factures', 'Facture.Generate');
      this.canReadClients = AuthService.canAccessModule(user, 'Clients', 'Client.Read');
      this.canUpdateClients = AuthService.canAccessModule(user, 'Clients', 'Client.Update');
      this.canAttachProof = AuthService.canAccessModule(user, 'Payments', 'Payment.Update');

      if (!this.canUpdateClients) {
        this.clientForm.disable();
      }

      // Only load what the user can actually act on; the endpoints are gated the
      // same way and would 403 otherwise.
      if (this.canGenerateContracts) {
        this.loadTemplates(DocumentTemplateKind.Contract);
      }
      if (this.canGenerateFactures) {
        this.loadTemplates(DocumentTemplateKind.Facture);
      }

      const idParam = this.route.snapshot.paramMap.get('id');
      if (idParam) {
        this.rentingId = +idParam;
        // Every car, including one since put in maintenance: the renting may
        // already be on it, and UpdateRentingCommand allows keeping it.
        this.loadCars(null);
        this.reload();
        if (this.canUseExtraServices) {
          this.extraServiceTypesClient.getExtraServiceTypes(true).subscribe({
            next: types => this.extraServiceTypes = types || [],
            error: err => console.error(err)
          });
        }
      } else {
        // Creating: offering a car the API will refuse is a trap, so the picker
        // only lists the ones that can actually be booked.
        this.loadCars(CarStatus.Active);
        this.applyPrefill();
      }
    });
  }

  // Opened from a row that already answers part of the wizard: the cars list
  // knows the car (?carId), the client list knows the renter (?clientId). Only
  // the choice is made here — the dates, the price and the availability re-check
  // for the period asked all still happen in the wizard, so nothing is skipped.
  private applyPrefill() {
    const params = this.route.snapshot.queryParamMap;
    const carId = Number(params.get('carId'));
    const clientId = Number(params.get('clientId'));

    if (Number.isInteger(carId) && carId > 0) {
      this.vehicleGroup.patchValue({ carId });
    }

    if (Number.isInteger(clientId) && clientId > 0) {
      // Explicitly the existing-client branch: a prefilled id would otherwise sit
      // unused behind the "new client" panel if that were ever the default.
      this.clientGroup.patchValue({ clientMode: 'existing', clientId });
    }
  }

  private loadCars(status: CarStatus | null) {
    this.carsClient.getCars(1, 1000, null, null, null, status, null, null, null, null, false).subscribe({
      next: r => {
        this.cars = r.items || [];
        // A car prefilled from the URL (?carId, from the fleet's "rent this car")
        // was chosen before its record was here to read, so the reading it should
        // start from is offered now.
        this.offerCarMileage();
      },
      error: err => console.error(err)
    });
  }

  /**
   * Offers the selected car's odometer as this hire's pickup reading — the
   * mileage the counter would otherwise copy off the dashboard, already typed in
   * and still editable. Only for a new booking: whatever was recorded on an
   * existing renting is what actually happened and is not overwritten. A car
   * with no reading on file leaves the field as it is rather than blanking it.
   */
  private offerCarMileage() {
    if (this.isEdit) return;

    const mileage = this.selectedCar?.mileage;
    if (mileage === null || mileage === undefined) return;

    this.vehicleGroup.get('startMileage')!.setValue(mileage);
  }

  // Creating a client inline is a create-time convenience; editing an existing
  // renting reassigns it to an already-known client.
  private applyClientMode(mode: string) {
    const clientId = this.clientGroup.get('clientId')!;
    const newClient = this.clientGroup.get('newClient')!;

    if (mode === 'new') {
      clientId.clearValidators();
      clientId.setValue(null);
      newClient.enable();
    } else {
      clientId.setValidators(Validators.required);
      newClient.disable();
    }

    clientId.updateValueAndValidity();
  }

  // Three states, so the two inactive sources are both cleared and both left
  // unvalidated — a half-filled "new second driver" must not block a save that
  // ended up with no second driver at all.
  private applySecondMode(mode: string) {
    const secondClientId = this.clientGroup.get('secondClientId')!;
    const secondNewClient = this.clientGroup.get('secondNewClient')!;

    if (mode === 'new') {
      secondClientId.setValue(null, { emitEvent: false });
      secondClientId.clearValidators();
      secondNewClient.enable({ emitEvent: false });
    } else {
      if (mode === 'none') {
        secondClientId.setValue(null, { emitEvent: false });
        secondClientId.clearValidators();
      } else {
        // Having asked for an existing client, the save must not quietly go
        // through with nobody picked — that would drop the second driver.
        secondClientId.setValidators(Validators.required);
      }
      secondNewClient.disable({ emitEvent: false });
    }

    secondClientId.updateValueAndValidity({ emitEvent: false });
  }

  // Turning the override on seeds the input with the figure currently on screen,
  // so adjusting a price starts from it rather than from an empty box. Turning it
  // off drops the value: an empty PriceOverride is what asks the API to quote.
  private applyPriceMode(on: boolean) {
    const manual = this.vehicleGroup.get('manualPrice')!;

    if (on) {
      const seed = this.quotedAmount ?? this.renting?.price?.amount ?? null;
      if (manual.value === null || manual.value === '') {
        manual.setValue(seed === null ? null : Number(seed.toFixed(2)));
      }
      manual.setValidators([Validators.required, Validators.min(0)]);
    } else {
      manual.setValue(null);
      manual.setValidators(Validators.min(0));
    }

    manual.updateValueAndValidity({ emitEvent: false });
  }

  // The agency's layouts for this kind of document, plus what the template that
  // would actually be used needs the agent to type in.
  private loadTemplates(kind: DocumentTemplateKind) {
    this.templatesClient.getDocumentTemplates(kind, null, false).subscribe({
      next: list => {
        if (kind === DocumentTemplateKind.Contract) {
          this.contractTemplates = list || [];
        } else {
          this.factureTemplates = list || [];
        }
        this.loadPrompts(kind);
      },
      error: err => console.error(err)
    });
  }

  // Re-asked whenever the chosen template changes: a different layout asks for
  // different things, and prompting for the wrong template's fields would be worse
  // than not prompting.
  loadPrompts(kind: DocumentTemplateKind) {
    const templateId = kind === DocumentTemplateKind.Contract
      ? this.contractTemplateId
      : this.factureTemplateId;

    this.templatesClient.getDocumentPrompt(kind, templateId).subscribe({
      next: fields => {
        if (kind === DocumentTemplateKind.Contract) {
          this.contractPrompts = fields || [];
        } else {
          this.facturePrompts = fields || [];
        }

        // Seed any new placeholder so the input binds to a defined value.
        for (const field of fields || []) {
          if (field.placeholder && this.documentValues[field.placeholder] === undefined) {
            this.documentValues[field.placeholder] = '';
          }
        }
      },
      error: err => console.error(err)
    });
  }

  /** The prompts to show, without asking for the same placeholder twice. */
  get activePrompts(): DocumentTemplateFieldDto[] {
    const wanted: DocumentTemplateFieldDto[] = [];

    const include = (fields: DocumentTemplateFieldDto[]) => {
      for (const field of fields) {
        if (!wanted.some(f => f.placeholder === field.placeholder)) wanted.push(field);
      }
    };

    if (this.isEdit || this.paperworkGroup.get('generateContract')?.value) include(this.contractPrompts);
    if (this.isEdit || this.paperworkGroup.get('generateFacture')?.value) include(this.facturePrompts);

    return wanted;
  }

  // Loads the picked client's own record so the agent can check and correct it
  // (and their papers) without losing the booking they are in the middle of.
  private loadSelectedClient(clientId: number | null) {
    this.clientSavedMessage = '';

    if (!clientId || !this.canReadClients) {
      this.selectedClient = undefined;
      this.clientDocuments.forEach(slot => slot.url = undefined);
      return;
    }

    this.loadingClient = true;
    this.clientsClient.getClientById(clientId).subscribe({
      next: dto => {
        this.loadingClient = false;
        this.selectedClient = dto;
        this.clientForm.patchValue({
          firstName: dto.firstName ?? '',
          lastName: dto.lastName ?? '',
          birthDate: toDateInput(dto.birthDate),
          birthPlace: dto.birthPlace ?? '',
          cin: dto.cin ?? '',
          drivingLicenceNumber: dto.drivingLicenceNumber ?? '',
          passeportNumber: dto.passeportNumber ?? '',
          description: dto.description ?? ''
        });
        this.clientDocuments[0].url = dto.cinImageUrl;
        this.clientDocuments[1].url = dto.drivingLicenceImageUrl;
        this.clientDocuments[2].url = dto.passerportImageUrl;
      },
      error: err => {
        this.loadingClient = false;
        // A client the agent may book with but not read is not an error worth a
        // banner — the panel simply stays closed.
        this.selectedClient = undefined;
        console.error(err);
      }
    });
  }

  toggleClientPanel() {
    this.clientPanelOpen = !this.clientPanelOpen;
  }

  // The API's client update is a full replace, so the fields this compact panel
  // does not show (document issue dates, places and countries) are carried
  // through from what was loaded. Editing a client here must never blank the
  // parts of their file that live on the full client page.
  saveSelectedClient() {
    if (!this.selectedClient?.id || !this.canUpdateClients) return;

    if (this.clientForm.invalid) {
      this.clientForm.markAllAsTouched();
      return;
    }

    this.savingClient = true;
    this.errorMessage = '';
    this.clientSavedMessage = '';

    const v = this.clientForm.getRawValue();
    const loaded = this.selectedClient;

    const command = new UpdateClientCommand({
      id: loaded.id,
      rowVersion: loaded.rowVersion,
      firstName: v.firstName,
      lastName: v.lastName,
      birthDate: fromDateInput(v.birthDate),
      birthPlace: v.birthPlace || undefined,
      cin: v.cin || undefined,
      drivingLicenceNumber: v.drivingLicenceNumber || undefined,
      passeportNumber: v.passeportNumber || undefined,
      description: v.description || undefined,
      // Carried through unchanged — see the comment above.
      birthCountryId: loaded.birthCountryId,
      cinDeliveranceDate: loaded.cinDeliveranceDate,
      cinDeliverancePlace: loaded.cinDeliverancePlace,
      cinDeliveranceCountryId: loaded.cinDeliveranceCountryId,
      passeportDeliveranceDate: loaded.passeportDeliveranceDate,
      passeportDeliverancePlace: loaded.passeportDeliverancePlace,
      passeportDeliveranceCountryId: loaded.passeportDeliveranceCountryId,
      drivingLicenceDeliveranceDate: loaded.drivingLicenceDeliveranceDate,
      drivingLicenceDeliverancePlace: loaded.drivingLicenceDeliverancePlace,
      drivingLicenceDeliveranceCountryId: loaded.drivingLicenceDeliveranceCountryId
    });

    this.clientsClient.updateClient(loaded.id, command).subscribe({
      next: () => {
        this.savingClient = false;
        this.clientSavedMessage = this.transloco.translate('renting.clientSaved');
        // Re-read for a fresh concurrency token, and refresh the picker so the
        // dropdown shows the corrected name.
        this.loadSelectedClient(loaded.id!);
        this.reloadClientList();
      },
      error: err => {
        this.savingClient = false;
        this.handleError(err);
      }
    });
  }

  // Uploading a paper replaces the previous one server-side; Client.Update is
  // the permission for it, same as editing the record.
  onClientDocumentSelected(slot: ClientDocumentSlot, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !this.selectedClient?.id || !this.canUpdateClients) return;

    slot.uploading = true;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.clientsClient.uploadClientDocument(this.selectedClient.id, slot.type, parameter).subscribe({
      next: url => {
        slot.url = url;
        slot.uploading = false;
      },
      error: err => {
        slot.uploading = false;
        this.handleError(err);
      }
    });
  }

  private reloadClientList() {
    this.clientsClient.getClients(1, 1000, null, null, null, null, null, null, false).subscribe({
      next: r => this.clients = r.items || [],
      error: err => console.error(err)
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
          vehicle: {
            carId: dto.carId ?? null,
            startDate: toDateTimeInput(dto.startDate),
            endDate: toDateTimeInput(dto.endDate),
            startMileage: dto.startMileage ?? null,
            endMileage: dto.endMileage ?? null,
            // Reloading discards an unsaved price adjustment along with every
            // other unsaved edit.
            overridePrice: false,
            manualPrice: null
          },
          client: {
            clientMode: 'existing',
            clientId: dto.clientId ?? null,
            // A booking that already has a second driver opens on the picker
            // holding them; one that has none opens on "none".
            secondMode: dto.secondClientId ? 'existing' : 'none',
            secondClientId: dto.secondClientId ?? null
          },
          paperwork: {
            notes: dto.notes ?? ''
          }
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
    if (this.canReadContracts) {
      this.contractsClient.getContractsByRenting(this.rentingId).subscribe({
        next: list => this.contracts = list || [],
        error: err => console.error(err)
      });
    }
    if (this.canReadFactures) {
      this.facturesClient.getFacturesByRenting(this.rentingId).subscribe({
        next: list => this.factures = list || [],
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
    const vehicle = v.vehicle;
    const client = v.client;

    // Absent unless the agent asked to set the price by hand — an absent
    // PriceOverride is what tells the API to quote (see CreateRentingCommand).
    const priceOverride = vehicle.overridePrice && vehicle.manualPrice !== null && vehicle.manualPrice !== ''
      ? Number(vehicle.manualPrice)
      : undefined;

    if (this.isEdit) {
      const command = new UpdateRentingCommand({
        id: this.rentingId,
        rowVersion: this.rowVersion,
        carId: vehicle.carId,
        clientId: client.clientId,
        // At most one second-driver source; neither removes the second driver.
        secondClientId: this.secondSelectedId(client),
        secondNewClient: this.secondPayload(client),
        startDate: fromDateInput(vehicle.startDate),
        endDate: fromDateInput(vehicle.endDate),
        startMileage: vehicle.startMileage ?? undefined,
        endMileage: vehicle.endMileage ?? undefined,
        priceOverride,
        notes: v.paperwork.notes || undefined
      });
      this.client.updateRenting(this.rentingId!, command).subscribe({
        next: () => {
          this.saving = false;
          this.reload();
          // The save may have created a second driver, who has to be in the list
          // before the reloaded picker can show their name rather than a blank.
          this.reloadClientList();
        },
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateRentingCommand({
        carId: vehicle.carId,
        // Exactly one of the two — the API rejects both or neither.
        clientId: this.isNewClient ? undefined : client.clientId,
        newClient: this.isNewClient ? this.toNewClient(client.newClient) : undefined,
        secondClientId: this.secondSelectedId(client),
        secondNewClient: this.secondPayload(client),
        startDate: fromDateInput(vehicle.startDate),
        endDate: fromDateInput(vehicle.endDate),
        startMileage: vehicle.startMileage ?? undefined,
        priceOverride,
        notes: v.paperwork.notes || undefined,
        // Only ever requested when the agency has the feature and the user the
        // permission; the API enforces the same pair.
        generateContract: this.canGenerateContracts && !!v.paperwork.generateContract,
        generateFacture: this.canGenerateFactures && !!v.paperwork.generateFacture,
        contractTemplateId: this.contractTemplateId ?? undefined,
        factureTemplateId: this.factureTemplateId ?? undefined,
        documentValues: this.filledDocumentValues()
      });
      this.client.createRenting(command).subscribe({
        next: id => this.router.navigate(['/renting', id]),
        error: err => this.handleError(err)
      });
    }
  }

  private toNewClient(value: any): NewRentingClient {
    return new NewRentingClient({
      firstName: value.firstName,
      lastName: value.lastName,
      email: value.email || undefined,
      birthDate: fromDateInput(value.birthDate),
      cin: value.cin || undefined,
      drivingLicenceNumber: value.drivingLicenceNumber || undefined,
      passeportNumber: value.passeportNumber || undefined,
      description: value.description || undefined
    });
  }

  // The two second-driver fields the API accepts, derived from the mode. Both
  // undefined means the booking has no second driver — which is also how an
  // existing one is taken off it.
  private secondSelectedId(client: any): number | undefined {
    return this.secondMode === 'existing' ? client.secondClientId ?? undefined : undefined;
  }

  private secondPayload(client: any): NewRentingClient | undefined {
    // No email: it would be stored but provision nothing, because the customer
    // portal lists a person's bookings as the renter (see NewRentingClient).
    return this.secondMode === 'new' ? this.toNewClient(client.secondNewClient) : undefined;
  }

  // --- Change end date -----------------------------------------------------

  openEndDatePanel() {
    this.newEndDate = toDateTimeInput(this.renting?.endDate);
    // Without the permission there is no choice to make: the renting changes and
    // the existing paperwork stays as it is.
    this.reissueContract = this.canGenerateContracts;
    this.overrideEndDatePrice = false;
    this.endDatePrice = null;
    this.errorMessage = '';
    this.endDatePanelOpen = true;
  }

  closeEndDatePanel() {
    this.endDatePanelOpen = false;
  }

  /**
   * True as soon as the moment picked is not the booked one — including a move of
   * a few hours, which the day count below rounds away but which the agency may
   * well want on the record.
   */
  get endDateChanged(): boolean {
    return !!this.endDateDelta;
  }

  /** Whole days between the current end date and the one picked; null when unset. */
  get endDateDeltaDays(): number | null {
    const delta = this.endDateDelta;
    if (delta === null) return null;

    const days = Math.round(delta / 86_400_000);
    return days === 0 ? null : days;
  }

  // Milliseconds between the two; null while either is unset.
  private get endDateDelta(): number | null {
    if (!this.newEndDate || !this.renting?.endDate) return null;

    // Both sides are the same kind of string — the wall clock the field shows —
    // so the difference is the one on screen rather than a timezone offset.
    const current = new Date(toDateTimeInput(this.renting.endDate)).getTime();
    const next = new Date(this.newEndDate).getTime();
    if (isNaN(current) || isNaN(next)) return null;

    return next - current;
  }

  // Seeds the amended total with the price as it stands, so a negotiated figure
  // is typed over a real number rather than into an empty box.
  onOverrideEndDatePrice(on: boolean) {
    this.overrideEndDatePrice = on;
    this.endDatePrice = on ? this.renting?.price?.amount ?? null : null;
  }

  changeEndDate() {
    if (!this.rentingId || !this.newEndDate) return;

    const endDate = fromDateInput(this.newEndDate);
    if (!endDate) return;

    this.changingEndDate = true;
    this.errorMessage = '';

    const command = new ChangeRentingEndDateCommand({
      id: this.rentingId,
      rowVersion: this.renting?.rowVersion,
      endDate,
      priceOverride: this.overrideEndDatePrice && this.endDatePrice !== null
        ? Number(this.endDatePrice)
        : undefined,
      regenerateContract: this.reissueContract && this.canGenerateContracts,
      contractTemplateId: this.contractTemplateId ?? undefined,
      documentValues: this.filledDocumentValues()
    });

    this.client.changeRentingEndDate(this.rentingId, command).subscribe({
      next: () => {
        this.changingEndDate = false;
        this.endDatePanelOpen = false;
        // Reload rather than patch: the price changed, and a reissued contract is
        // a new row in the documents list.
        this.reload();
      },
      error: err => {
        this.changingEndDate = false;
        this.handleError(err);
      }
    });
  }

  generateContract() {
    if (!this.rentingId) return;
    this.generatingContract = true;
    this.errorMessage = '';

    const command = new GenerateContractCommand({
      rentingId: this.rentingId,
      templateId: this.contractTemplateId ?? undefined,
      manualValues: this.filledDocumentValues()
    });

    this.contractsClient.generateContract(this.rentingId, command).subscribe({
      next: () => {
        this.generatingContract = false;
        this.reload();
      },
      error: err => {
        this.generatingContract = false;
        this.handleError(err);
      }
    });
  }

  generateFacture() {
    if (!this.rentingId) return;
    this.generatingFacture = true;
    this.errorMessage = '';

    const command = new GenerateFactureCommand({
      rentingId: this.rentingId,
      templateId: this.factureTemplateId ?? undefined,
      manualValues: this.filledDocumentValues()
    });

    this.facturesClient.generateFacture(this.rentingId, command).subscribe({
      next: () => {
        this.generatingFacture = false;
        this.reload();
      },
      error: err => {
        this.generatingFacture = false;
        this.handleError(err);
      }
    });
  }

  // Blank entries are dropped rather than sent as empty strings, so the server's
  // "required and missing" check sees the truth.
  private filledDocumentValues(): { [key: string]: string } | undefined {
    const filled: { [key: string]: string } = {};

    for (const [placeholder, value] of Object.entries(this.documentValues)) {
      if (value && value.trim().length > 0) filled[placeholder] = value;
    }

    return Object.keys(filled).length > 0 ? filled : undefined;
  }

  downloadContract(contract: ContractDto) {
    if (contract.id) this.openPdf(`/api/Contracts/${contract.id}/download`, `${contract.number}.pdf`);
  }

  downloadFacture(facture: FactureDto) {
    if (facture.id) this.openPdf(`/api/Factures/${facture.id}/download`, `${facture.number}.pdf`);
  }

  // Fetched as a blob rather than linked to directly: the download route is
  // permission-checked, and going through HttpClient keeps the auth / language /
  // impersonation interceptors on the request. The generated NSwag method
  // discards the body (the endpoint has no JSON schema), hence the raw call.
  private openPdf(url: string, fileName: string) {
    this.errorMessage = '';
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: blob => {
        const objectUrl = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = fileName;
        link.click();
        // Revoking immediately would race the click on some browsers.
        setTimeout(() => URL.revokeObjectURL(objectUrl), 10000);
      },
      error: err => this.handleError(err)
    });
  }

  startRenting() {
    // Offered: what the booking already recorded, or the car's odometer if it was
    // booked without a reading. The agent overtypes it with what the dashboard
    // actually says, which is also what moves the car's own figure on.
    const offered = this.renting?.startMileage ?? this.selectedCar?.mileage;

    const value = prompt(
      this.transloco.translate('renting.promptPickupMileage'),
      offered === null || offered === undefined ? '' : String(offered));

    if (value === null) return; // cancelled
    const mileage = value.trim() === '' ? undefined : Number(value);
    this.changeState(RentingState.InProgress, mileage);
  }

  // The return is the one transition with more than one thing to say — the
  // odometer, and the day the car actually came back (which re-prices the hire) —
  // so it gets the dialog every other screen closes a hire through.
  completeRenting() {
    if (!this.rentingId) return;

    this.dialog.open(ReturnDialogComponent, {
      data: {
        rentingId: this.rentingId,
        carLabel: [this.renting?.carMatricule, this.renting?.carModelName].filter(Boolean).join(' · '),
        clientName: this.renting?.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(returned => {
      if (returned) this.reload();
    });
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

  // The same dialog the list cancels through: what the client still owes and what
  // goes back to them are decisions, not a yes/no (see CancelDialogComponent).
  cancelRenting() {
    if (!this.rentingId) return;

    this.dialog.open(CancelDialogComponent, {
      data: {
        rentingId: this.rentingId,
        carLabel: [this.renting?.carMatricule, this.renting?.carModelName].filter(Boolean).join(' · '),
        clientName: this.renting?.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(cancelled => {
      if (cancelled) this.reload();
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

  // Attaches (or replaces) the receipt / slip / invoice kept against an entry.
  onProofSelected(payment: PaymentDto, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !payment.id) return;

    this.uploadingProofFor = payment.id;
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.paymentsClient.uploadPaymentProof(payment.id, parameter).subscribe({
      next: () => {
        this.uploadingProofFor = null;
        this.reload();
      },
      error: err => {
        this.uploadingProofFor = null;
        this.handleError(err);
      }
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

  /** What has been paid so far, for the money summary in edit mode. */
  get paidAmount(): number {
    return this.payments.reduce((sum, p) => sum + (p.payementAmount?.amount ?? 0), 0);
  }

  /**
   * What this booking charges. A cancelled one charges its cancellation fee
   * instead of its price — nothing at all when it was called off for free — which
   * is the same rule the client's balance and the credits screen apply (see
   * ClientCreditRows). Extras go with it: a hire that did not happen does not
   * bill the services that were to come with it.
   */
  get chargeAmount(): number {
    if (this.renting?.rentingState === RentingState.Cancelled) {
      return this.renting?.cancellationFee?.amount ?? 0;
    }

    const extras = this.extraServices.reduce((sum, e) => sum + (e.totalAmount?.amount ?? 0), 0);
    return (this.renting?.price?.amount ?? 0) + extras;
  }

  /** Still owed on this booking; negative means the agency owes it back. */
  get balanceDue(): number {
    return this.chargeAmount - this.paidAmount;
  }

  /** What the client has paid beyond what the booking charges, as a credit. */
  get creditDue(): number {
    return Math.max(0, -this.balanceDue);
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

  /** Tone class for the state chip, matching the list screen's colours. */
  stateClass(state?: RentingState): string {
    switch (state) {
      case RentingState.NotYet: return 'info';
      case RentingState.InProgress: return 'ok';
      case RentingState.Done: return 'neutral';
      case RentingState.Cancelled: return 'danger';
      default: return 'neutral';
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
