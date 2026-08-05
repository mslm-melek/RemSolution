import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ClientsClient, CountriesClient, CountryDto, ClientDto,
  CreateClientCommand, UpdateClientCommand, ClientDocumentType, FileParameter,
  ClientAccountOutcome
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
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
  form: FormGroup;
  countries: CountryDto[] = [];
  clientId?: number;
  saving = false;
  errorMessage = '';

  // The client's money (balance, entries, taking a payment) lives on the client's
  // own page now — see ClientDetailComponent — so this stays a form.

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

  // The face cut out of the CIN, derived server-side on upload. Kept here so the
  // person who uploaded the card can see what was read off it.
  portraitUrl?: string;
  recroppingPortrait = false;

  constructor(
    private fb: FormBuilder,
    private client: ClientsClient,
    private countriesClient: CountriesClient,
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
    this.portraitUrl = dto.cinPortraitUrl;

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
        // Land back on this form (now in edit mode) so documents can be uploaded
        // right away — the client's page is a step away once that is done.
        next: id => this.router.navigate(['/client', id, 'edit']),
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

        // A new CIN brings a new portrait with it (or clears the old one), and the
        // upload only answers with the document's own URL. Reading the client back
        // is what shows which of the two happened.
        if (slot.type === ClientDocumentType.CIN) this.refreshPortrait();
      },
      error: err => {
        slot.uploading = false;
        this.handleError(err);
      }
    });
  }

  /** Re-cuts the portrait out of the CIN on file — see RegenerateClientPortraitCommand. */
  recropPortrait() {
    if (!this.clientId || this.recroppingPortrait) return;

    this.recroppingPortrait = true;
    this.errorMessage = '';

    this.client.regenerateClientPortrait(this.clientId).subscribe({
      next: result => {
        this.portraitUrl = result.portraitUrl;
        this.recroppingPortrait = false;
      },
      error: err => {
        this.recroppingPortrait = false;
        this.handleError(err);
      }
    });
  }

  // Only the portrait, not the whole form: the user may have typed into the fields
  // while the upload was in flight, and re-patching the form would discard that.
  private refreshPortrait() {
    if (!this.clientId) return;

    this.client.getClientById(this.clientId).subscribe({
      next: dto => this.portraitUrl = dto.cinPortraitUrl,
      error: err => console.error(err)
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
