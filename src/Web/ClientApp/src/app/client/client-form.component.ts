import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ClientsClient, CountriesClient, CountryDto, ClientDto,
  CreateClientCommand, UpdateClientCommand, ClientDocumentType, FileParameter,
  ClientAccountOutcome, PaymentsClient, PaymentDto, ClientBalanceDto, PaymentMethod
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';
import { TranslocoService } from '@jsverse/transloco';

interface DocumentSlot {
  type: ClientDocumentType;
  labelKey: string;
  url?: string;
  uploading: boolean;
}

@Component({
  selector: 'app-client-form',
  templateUrl: './client-form.component.html',
  styleUrls: ['./client-form.component.css']
})
export class ClientFormComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  form: FormGroup;
  countries: CountryDto[] = [];
  clientId?: number;
  saving = false;
  errorMessage = '';

  // Money panel: the client's position with the agency and the entries behind
  // it, so a counter payment can be taken from the client's own page.
  balance?: ClientBalanceDto;
  payments: PaymentDto[] = [];
  paymentColumns = ['date', 'amount', 'method', 'proof'];
  canReadPayments = false;
  canPay = false;
  canAttachProof = false;
  // Payment id whose proof is uploading, so only its own button waits.
  uploadingProofFor: number | null = null;

  paymentMethods = [
    { value: PaymentMethod.Cash, labelKey: 'enums.paymentMethod.cash' },
    { value: PaymentMethod.Card, labelKey: 'enums.paymentMethod.card' },
    { value: PaymentMethod.Transfer, labelKey: 'enums.paymentMethod.transfer' },
    { value: PaymentMethod.Cheque, labelKey: 'enums.paymentMethod.cheque' }
  ];

  // Customer-portal account state, refreshed from the invite response so the
  // panel is right without re-fetching the whole client.
  hasPortalAccount = false;
  inviting = false;
  inviteMessage = '';
  // Distinguishes "we could not do what you asked" from "done": both land in
  // inviteMessage, and only one of them should read as a success.
  inviteWarning = false;

  // Optimistic-concurrency token read with the client and echoed back on update.
  private rowVersion?: string;

  documents: DocumentSlot[] = [
    { type: ClientDocumentType.CIN, labelKey: 'client.cin', uploading: false },
    { type: ClientDocumentType.DrivingLicence, labelKey: 'client.drivingLicence', uploading: false },
    { type: ClientDocumentType.Passeport, labelKey: 'client.passeport', uploading: false }
  ];

  constructor(
    private fb: FormBuilder,
    private client: ClientsClient,
    private countriesClient: CountriesClient,
    private paymentsClient: PaymentsClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      // Optional, but saving one provisions the client's portal login — the
      // server mirrors this rule, this is only the early feedback.
      email: ['', [Validators.email, Validators.maxLength(256)]],
      birthDate: ['', Validators.required],
      birthPlace: [''],
      birthCountryId: [null],
      cin: [''],
      cinDeliveranceDate: [''],
      cinDeliverancePlace: [''],
      cinDeliveranceCountryId: [null],
      passeportNumber: [''],
      passeportDeliveranceDate: [''],
      passeportDeliverancePlace: [''],
      passeportDeliveranceCountryId: [null],
      drivingLicenceNumber: [''],
      drivingLicenceDeliveranceDate: [''],
      drivingLicenceDeliverancePlace: [''],
      drivingLicenceDeliveranceCountryId: [null],
      description: ['']
    });
  }

  get isEdit(): boolean {
    return this.clientId !== undefined;
  }

  ngOnInit() {
    this.countriesClient.getCountries().subscribe({
      next: countries => this.countries = countries || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.clientId = +idParam;
      this.client.getClientById(this.clientId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
    }

    this.auth.currentUser$.subscribe(user => {
      this.canReadPayments = AuthService.canAccessModule(user, 'Payments', 'Payment.Read');
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
      this.canAttachProof = AuthService.canAccessModule(user, 'Payments', 'Payment.Update');

      if (this.isEdit && this.canReadPayments) this.loadMoney();
    });
  }

  // The client's position and the entries behind it. Read together: the balance
  // is the figure that matters, the entries are how it got there.
  private loadMoney() {
    if (!this.clientId) return;

    this.paymentsClient.getClientBalance(this.clientId).subscribe({
      next: balance => this.balance = balance,
      error: err => console.error(err)
    });

    this.paymentsClient.getPayments(1, 50, null, this.clientId, null).subscribe({
      next: result => this.payments = result.items || [],
      error: err => console.error(err)
    });
  }

  // Takes money against the client's overall balance rather than one booking —
  // what a counter payment settling arrears actually is.
  pay() {
    if (!this.clientId) return;

    const name = `${this.form.value.firstName ?? ''} ${this.form.value.lastName ?? ''}`.trim();

    this.dialog.open(PaymentDialogComponent, {
      data: {
        target: { kind: 'client', id: this.clientId },
        subtitle: name || undefined,
        outstanding: this.balance?.balance?.amount,
        currency: this.balance?.currency
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(recorded => {
      if (recorded) this.loadMoney();
    });
  }

  // Attaching the proof to an entry already recorded (the dialog offers it at
  // the moment of payment; this is for the ones taken before, or by phone).
  onProofSelected(payment: PaymentDto, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !payment.id) return;

    this.uploadingProofFor = payment.id;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.paymentsClient.uploadPaymentProof(payment.id, parameter).subscribe({
      next: () => {
        this.uploadingProofFor = null;
        this.loadMoney();
      },
      error: err => {
        this.uploadingProofFor = null;
        this.handleError(err);
      }
    });
  }

  // Returns a transloco key; the template pipes it.
  methodLabelKey(method?: PaymentMethod): string {
    return this.paymentMethods.find(m => m.value === method)?.labelKey ?? '';
  }

  private populate(dto: ClientDto) {
    this.form.patchValue({
      firstName: dto.firstName ?? '',
      lastName: dto.lastName ?? '',
      email: dto.email ?? '',
      birthDate: toDateInput(dto.birthDate),
      birthPlace: dto.birthPlace ?? '',
      birthCountryId: dto.birthCountryId ?? null,
      cin: dto.cin ?? '',
      cinDeliveranceDate: toDateInput(dto.cinDeliveranceDate),
      cinDeliverancePlace: dto.cinDeliverancePlace ?? '',
      cinDeliveranceCountryId: dto.cinDeliveranceCountryId ?? null,
      passeportNumber: dto.passeportNumber ?? '',
      passeportDeliveranceDate: toDateInput(dto.passeportDeliveranceDate),
      passeportDeliverancePlace: dto.passeportDeliverancePlace ?? '',
      passeportDeliveranceCountryId: dto.passeportDeliveranceCountryId ?? null,
      drivingLicenceNumber: dto.drivingLicenceNumber ?? '',
      drivingLicenceDeliveranceDate: toDateInput(dto.drivingLicenceDeliveranceDate),
      drivingLicenceDeliverancePlace: dto.drivingLicenceDeliverancePlace ?? '',
      drivingLicenceDeliveranceCountryId: dto.drivingLicenceDeliveranceCountryId ?? null,
      description: dto.description ?? ''
    });

    this.documents[0].url = dto.cinImageUrl;
    this.documents[1].url = dto.drivingLicenceImageUrl;
    this.documents[2].url = dto.passerportImageUrl;

    this.hasPortalAccount = dto.hasPortalAccount === true;
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
      const command = new UpdateClientCommand({ id: this.clientId, rowVersion: this.rowVersion, ...payload });
      this.client.updateClient(this.clientId!, command).subscribe({
        next: () => this.router.navigate(['/client']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateClientCommand(payload);
      this.client.createClient(command).subscribe({
        // Land on the edit page so documents can be uploaded right away.
        next: id => this.router.navigate(['/client', id]),
        error: err => this.handleError(err)
      });
    }
  }

  // Sends, or re-sends, the customer's invitation. Every branch reports what
  // actually happened: an agency that clicks this is about to tell the customer
  // "check your email", and "there was nothing to send" or "the mail bounced
  // off our own server" have to reach them before they do.
  invite() {
    if (!this.clientId) return;

    this.inviting = true;
    this.inviteMessage = '';
    this.inviteWarning = false;

    this.client.inviteClient(this.clientId).subscribe({
      next: result => {
        this.inviting = false;

        if (result.outcome === ClientAccountOutcome.Created ||
            result.outcome === ClientAccountOutcome.Linked ||
            result.outcome === ClientAccountOutcome.AlreadyLinked ||
            result.outcome === ClientAccountOutcome.PasswordReset ||
            result.outcome === ClientAccountOutcome.AlreadyActive) {
          this.hasPortalAccount = true;
        }

        this.applyInviteOutcome(result.outcome, result.emailSent === true);
      },
      error: err => {
        this.inviting = false;
        this.handleError(err);
      }
    });
  }

  private applyInviteOutcome(outcome: ClientAccountOutcome | undefined, emailSent: boolean) {
    // The two outcomes that issue a temporary password are the only ones that
    // should have produced an email, so they are also the only ones where a
    // silent send failure would mislead.
    const issuedCredentials =
      outcome === ClientAccountOutcome.Created || outcome === ClientAccountOutcome.PasswordReset;

    if (issuedCredentials && !emailSent) {
      this.inviteWarning = true;
      this.inviteMessage = this.transloco.translate('client.inviteMailFailed');
      return;
    }

    switch (outcome) {
      case ClientAccountOutcome.Created:
        this.inviteMessage = this.transloco.translate('client.inviteCreated');
        break;
      case ClientAccountOutcome.PasswordReset:
        this.inviteMessage = this.transloco.translate('client.inviteResent');
        break;
      case ClientAccountOutcome.Linked:
      case ClientAccountOutcome.AlreadyLinked:
        this.inviteMessage = this.transloco.translate('client.inviteLinked');
        break;
      case ClientAccountOutcome.AlreadyActive:
        this.inviteMessage = this.transloco.translate('client.inviteAlreadyActive');
        break;
      case ClientAccountOutcome.EmailBelongsToStaff:
        this.inviteWarning = true;
        this.inviteMessage = this.transloco.translate('client.inviteStaffEmail');
        break;
      default:
        this.inviteWarning = true;
        this.inviteMessage = this.transloco.translate('client.accountNeedsEmail');
        break;
    }
  }

  onFileSelected(slot: DocumentSlot, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !this.clientId) return;

    slot.uploading = true;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.client.uploadClientDocument(this.clientId, slot.type, parameter).subscribe({
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

  private toPayload() {
    const v = this.form.value;
    return {
      firstName: v.firstName,
      lastName: v.lastName,
      email: v.email || undefined,
      birthDate: fromDateInput(v.birthDate),
      birthPlace: v.birthPlace || undefined,
      birthCountryId: v.birthCountryId ?? undefined,
      cin: v.cin || undefined,
      cinDeliveranceDate: fromDateInput(v.cinDeliveranceDate),
      cinDeliverancePlace: v.cinDeliverancePlace || undefined,
      cinDeliveranceCountryId: v.cinDeliveranceCountryId ?? undefined,
      passeportNumber: v.passeportNumber || undefined,
      passeportDeliveranceDate: fromDateInput(v.passeportDeliveranceDate),
      passeportDeliverancePlace: v.passeportDeliverancePlace || undefined,
      passeportDeliveranceCountryId: v.passeportDeliveranceCountryId ?? undefined,
      drivingLicenceNumber: v.drivingLicenceNumber || undefined,
      drivingLicenceDeliveranceDate: fromDateInput(v.drivingLicenceDeliveranceDate),
      drivingLicenceDeliverancePlace: v.drivingLicenceDeliverancePlace || undefined,
      drivingLicenceDeliveranceCountryId: v.drivingLicenceDeliveranceCountryId ?? undefined,
      description: v.description || undefined
    };
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage = this.transloco.translate('client.concurrency');
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
