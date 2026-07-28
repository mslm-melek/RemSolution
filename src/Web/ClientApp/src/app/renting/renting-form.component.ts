import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import {
  RentingsClient, RentingDto, CreateRentingCommand, UpdateRentingCommand,
  ChangeRentingStateCommand, ChangeRentingEndDateCommand, RentingState, RentingHistoryDto,
  CarsClient, CarDto, ClientsClient, ClientDto, NewRentingClient,
  UpdateClientCommand, ClientDocumentType, FileParameter,
  ExtraServicesClient, ExtraServiceDto, CreateExtraServiceCommand,
  ExtraServiceTypesClient, ExtraServicesTypeDto,
  PaymentsClient, PaymentDto, CreatePaymentCommand, PaymentMethod,
  ContractsClient, ContractDto, GenerateContractCommand,
  FacturesClient, FactureDto, GenerateFactureCommand,
  DocumentTemplatesClient, DocumentTemplateDto, DocumentTemplateFieldDto, DocumentTemplateKind
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { TranslocoService } from '@jsverse/transloco';

// One of the selected client's identity papers, as shown in the renting form.
interface ClientDocumentSlot {
  type: ClientDocumentType;
  labelKey: string;
  url?: string;
  uploading: boolean;
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
      carId: [null, Validators.required],
      // 'existing' picks from the client list; 'new' fills newClient below and
      // the API creates the client in the same transaction as the renting.
      clientMode: ['existing'],
      clientId: [null, Validators.required],
      newClient: this.fb.group({
        firstName: ['', [Validators.required, Validators.maxLength(100)]],
        lastName: ['', [Validators.required, Validators.maxLength(100)]],
        birthDate: ['', Validators.required],
        cin: [''],
        drivingLicenceNumber: [''],
        passeportNumber: [''],
        description: ['']
      }),
      secondClientId: [null],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      startMileage: [null, Validators.min(0)],
      endMileage: [null, Validators.min(0)],
      notes: [''],
      generateContract: [false],
      generateFacture: [false]
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
    this.form.get('clientMode')!.valueChanges.subscribe(mode => this.applyClientMode(mode));

    // Picking a different client swaps the panel's contents.
    this.form.get('clientId')!.valueChanges.subscribe(id => this.loadSelectedClient(id));
  }

  get isEdit(): boolean {
    return this.rentingId !== undefined;
  }

  get isTerminal(): boolean {
    return this.renting?.rentingState === RentingState.Done
      || this.renting?.rentingState === RentingState.Cancelled;
  }

  get isNewClient(): boolean {
    return this.form.get('clientMode')?.value === 'new';
  }

  ngOnInit() {
    this.carsClient.getCars(1, 1000, null, null, null, null, false).subscribe({
      next: r => this.cars = r.items || [],
      error: err => console.error(err)
    });
    this.clientsClient.getClients(1, 1000, null, null, null, false).subscribe({
      next: r => this.clients = r.items || [],
      error: err => console.error(err)
    });

    // Resolve the agency's features before loading the feature-gated panels.
    this.auth.currentUser$.subscribe(user => {
      this.canUseExtraServices = AuthService.canAccessModule(user, 'ExtraServices', 'ExtraService.Read');
      this.canUsePayments = AuthService.canAccessModule(user, 'Payments', 'Payment.Read');
      this.canReadContracts = AuthService.canAccessModule(user, 'Contracts', 'Contract.Read');
      this.canGenerateContracts = AuthService.canAccessModule(user, 'Contracts', 'Contract.Generate');
      this.canReadFactures = AuthService.canAccessModule(user, 'Factures', 'Facture.Read');
      this.canGenerateFactures = AuthService.canAccessModule(user, 'Factures', 'Facture.Generate');
      this.canReadClients = AuthService.canAccessModule(user, 'Clients', 'Client.Read');
      this.canUpdateClients = AuthService.canAccessModule(user, 'Clients', 'Client.Update');

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

  // Creating a client inline is a create-time convenience; editing an existing
  // renting reassigns it to an already-known client.
  private applyClientMode(mode: string) {
    const clientId = this.form.get('clientId')!;
    const newClient = this.form.get('newClient')!;

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

    if (this.isEdit || this.form.get('generateContract')?.value) include(this.contractPrompts);
    if (this.isEdit || this.form.get('generateFacture')?.value) include(this.facturePrompts);

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
    this.clientsClient.getClients(1, 1000, null, null, null, false).subscribe({
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
          carId: dto.carId ?? null,
          clientMode: 'existing',
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
        // Exactly one of the two — the API rejects both or neither.
        clientId: this.isNewClient ? undefined : v.clientId,
        newClient: this.isNewClient ? this.toNewClient(v.newClient) : undefined,
        secondClientId: v.secondClientId ?? undefined,
        startDate: fromDateInput(v.startDate),
        endDate: fromDateInput(v.endDate),
        startMileage: v.startMileage ?? undefined,
        notes: v.notes || undefined,
        // Only ever requested when the agency has the feature and the user the
        // permission; the API enforces the same pair.
        generateContract: this.canGenerateContracts && !!v.generateContract,
        generateFacture: this.canGenerateFactures && !!v.generateFacture,
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
      birthDate: fromDateInput(value.birthDate),
      cin: value.cin || undefined,
      drivingLicenceNumber: value.drivingLicenceNumber || undefined,
      passeportNumber: value.passeportNumber || undefined,
      description: value.description || undefined
    });
  }

  // --- Change end date -----------------------------------------------------

  openEndDatePanel() {
    this.newEndDate = toDateInput(this.renting?.endDate);
    // Without the permission there is no choice to make: the renting changes and
    // the existing paperwork stays as it is.
    this.reissueContract = this.canGenerateContracts;
    this.errorMessage = '';
    this.endDatePanelOpen = true;
  }

  closeEndDatePanel() {
    this.endDatePanelOpen = false;
  }

  /** Whole days between the current end date and the one typed; null when unset. */
  get endDateDeltaDays(): number | null {
    if (!this.newEndDate || !this.renting?.endDate) return null;

    const current = new Date(toDateInput(this.renting.endDate)).getTime();
    const next = new Date(this.newEndDate).getTime();
    if (isNaN(current) || isNaN(next)) return null;

    return Math.round((next - current) / 86_400_000);
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
