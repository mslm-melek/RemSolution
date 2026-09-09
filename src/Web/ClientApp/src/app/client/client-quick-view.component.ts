import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import {
  ClientsClient, ClientDto, ClientCreditDto, CreditsClient,
  EraseClientPersonalDataCommand, FlagClientCommand,
  RentingDto, RentingState, RentingsClient
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { LateNoticeService } from '../shared/late-notice.service';
import {
  ClientStanding, clientStanding, clientStandingClass, clientStandingLabelKey
} from '../shared/client-standing';
import { extractProblemDetail } from '../shared/form-utils';

export interface ClientQuickViewData {
  id: number;
}

export interface ClientQuickViewResult {
  /** Something was written while the panel was open — the list behind it is stale. */
  changed: boolean;
  /** What a late notice sent from in here did, for the list's banner to say. */
  noticeMessage?: string;
}

/** One paper the agency has to be able to produce, and whether it is on file. */
interface ClientPaper {
  labelKey: string;
  icon: string;
  /** The number typed on the form, where there is one. */
  reference?: string;
  /** The stored image, where one was uploaded. */
  url?: string;
  /** Whether its absence is what keeps the client out of good standing. */
  required: boolean;
}

/** How many hires the panel shows before sending the reader to the full record. */
const RECENT_RENTINGS = 3;

/**
 * A client read at a glance, beside the book they were clicked in: who they are,
 * which papers are on file, what they have hired and what they owe — and the
 * moves worth making without leaving the list.
 *
 * A slide-over rather than a centred box, for the reason the booking panel is
 * one: the list behind it is the context for what is being read, and a dialog
 * over the middle of the page hides exactly the row that was clicked. The edge is
 * chosen by the caller so it follows the reading direction.
 *
 * It is deliberately NOT the client's page. /client/:id is where the whole
 * history, the payment ledger and the identity papers live; this answers the
 * question the book is scanned for — "can I hand this person a car, and do they
 * owe me anything?" — and links to that page for the rest.
 *
 * The record is re-read on open rather than passed in, like the booking panel's:
 * a row that has been sitting on a list carries a stale flag and a stale
 * concurrency token, and the actions here are decided from both.
 */
@Component({
  selector: 'app-client-quick-view',
  templateUrl: './client-quick-view.component.html',
  styleUrls: ['./client-quick-view.component.css']
})
export class ClientQuickViewComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<ClientQuickViewData>(MAT_DIALOG_DATA);

  client?: ClientDto;
  /** What they owe. Absent without Credit.Read, which a counter can lack. */
  credit?: ClientCreditDto;
  /** The last few hires, newest first — this client's activity, in one word each. */
  recent: RentingDto[] = [];

  papers: ClientPaper[] = [];
  standing: ClientStanding = 'pending';
  standingLabelKey = '';
  standingClass = '';

  loading = true;
  saving = false;
  errorMessage = '';

  /** Set by every successful write; returned to the list so it reloads once. */
  private changed = false;
  private noticeMessage?: string;

  // The reason box, open only while a flag is being raised: a flag with no reason
  // is a note to nobody, and the field is the whole point of the action.
  flagging = false;
  flagReason = '';

  // The reason box for an erasure, open only while one is being confirmed. Same
  // shape as the flag box above, and for a stronger version of the same reason:
  // this one cannot be undone, so the note beside it is what the audit trail will
  // have to answer with later.
  erasing = false;
  eraseReason = '';

  canEdit = false;
  canDelete = false;
  canErase = false;
  canRent = false;
  canRemind = false;
  private canSeeCredit = false;
  /** Read by the template: "no hires yet" is only true for someone who can see them. */
  canSeeRentings = false;

  RentingState = RentingState;

  constructor(
    private clients: ClientsClient,
    private creditsClient: CreditsClient,
    private rentings: RentingsClient,
    private lateNotice: LateNoticeService,
    private auth: AuthService,
    private dialogRef: MatDialogRef<ClientQuickViewComponent, ClientQuickViewResult>
  ) { }

  ngOnInit() {
    // Escape and a click on the backdrop are the two ways out that do not go
    // through close(), and Material's own handling of them closes with no result
    // at all — which would throw away the fact that a flag was raised or a notice
    // sent in here. The caller opens with disableClose so both land here instead.
    this.dialogRef.backdropClick().subscribe(() => this.close());
    this.dialogRef.keydownEvents().subscribe(event => {
      if (event.key === 'Escape') this.close();
    });

    this.auth.currentUser$.subscribe(user => {
      this.canEdit = AuthService.canAccessModule(user, 'Clients', 'Client.Update');
      this.canDelete = AuthService.canAccessModule(user, 'Clients', 'Client.Delete');
      this.canErase = AuthService.canAccessModule(user, 'Clients', 'Client.Erase');
      this.canRent = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');
      this.canRemind = AuthService.canAccessModule(user, 'Notifications', 'Notification.Send');
      this.canSeeCredit = AuthService.canAccessModule(user, 'Credits', 'Credit.Read');
      this.canSeeRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');

      // Both sections belong to modules of their own, so they are only asked for
      // once the permissions have landed — and only then, rather than in the
      // reload below, so a permission refresh does not re-ask for everything.
      if (this.client) this.loadSideSections();
    });

    this.reload();
  }

  private reload() {
    this.clients.getClientById(this.data.id).subscribe({
      next: client => {
        this.client = client;
        this.describe(client);
        this.loading = false;
        this.loadSideSections();
      },
      error: err => {
        this.loading = false;
        this.errorMessage = extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }

  /** What the client's own record says, worked out once per read. */
  private describe(client: ClientDto) {
    this.standing = clientStanding(client);
    this.standingLabelKey = clientStandingLabelKey(this.standing);
    this.standingClass = clientStandingClass(this.standing);

    // The order a counter checks them in, and the order the standing is decided
    // from: the card, the licence, then the passport a foreign client also has.
    this.papers = [
      {
        labelKey: 'client.cin', icon: 'badge', required: true,
        reference: client.cin, url: client.cinImageUrl
      },
      {
        labelKey: 'client.drivingLicence', icon: 'directions_car', required: true,
        reference: client.drivingLicenceNumber, url: client.drivingLicenceImageUrl
      },
      {
        labelKey: 'client.passeport', icon: 'menu_book', required: false,
        reference: client.passeportNumber, url: client.passerportImageUrl
      }
    ];
  }

  // Money and history: each behind its own permission, and neither worth failing
  // the panel over — a counter without Credit.Read still needs to read the papers.
  private loadSideSections() {
    if (this.canSeeCredit && !this.credit) {
      this.creditsClient.getClientCreditsByIds([this.data.id]).pipe(
        catchError(() => of([] as ClientCreditDto[]))
      ).subscribe(rows => this.credit = (rows || [])[0]);
    }

    if (this.canSeeRentings && !this.recent.length) {
      this.rentings.getRentings(
        1, RECENT_RENTINGS, null, null, this.data.id, null, null, null,
        undefined, false, 'startDate', true
      ).pipe(
        catchError(() => of(undefined))
      ).subscribe(result => this.recent = result?.items || []);
    }
  }

  // --- What the template reads ----------------------------------------------

  get name(): string {
    return `${this.client?.firstName ?? ''} ${this.client?.lastName ?? ''}`.trim();
  }

  get isLate(): boolean {
    return (this.client?.overdueRentingCount ?? 0) > 0;
  }

  /** Their details are gone, so the panel says so instead of looking broken. */
  get isErased(): boolean {
    return !!this.client?.personalDataErasedAt;
  }

  get owes(): boolean {
    return (this.credit?.outstanding?.amount ?? 0) > 0;
  }

  stateLabelKey(state: RentingState | undefined): string {
    switch (state) {
      case RentingState.NotYet: return 'enums.rentingState.notYet';
      case RentingState.InProgress: return 'enums.rentingState.inProgress';
      case RentingState.Done: return 'enums.rentingState.done';
      case RentingState.Cancelled: return 'enums.rentingState.cancelled';
      default: return '';
    }
  }

  // --- Actions --------------------------------------------------------------

  /** Opens the reason box; nothing is written until it is confirmed. */
  startFlagging() {
    this.flagging = true;
    this.flagReason = this.client?.notes ?? '';
  }

  cancelFlagging() {
    this.flagging = false;
    this.flagReason = '';
  }

  /** Raises the flag with the reason typed beside it (see FlagClientCommand). */
  raiseFlag() {
    this.flag(true, this.flagReason.trim() || undefined);
  }

  /** Clears the flag and the reason with it: the note explained a flag that is gone. */
  clearFlag() {
    this.flag(false, undefined);
  }

  private flag(isFlagged: boolean, notes?: string) {
    if (!this.client?.id || this.saving) return;

    this.saving = true;
    this.errorMessage = '';

    const command = new FlagClientCommand({ id: this.client.id, isFlagged, notes });

    this.clients.flagClient(this.client.id, command).subscribe({
      next: () => {
        this.saving = false;
        this.flagging = false;
        this.flagReason = '';
        this.changed = true;
        // Re-read rather than patch the copy in hand: the flag is what the
        // standing above is derived from, and the record carries a concurrency
        // token the next write needs.
        this.reload();
      },
      error: err => {
        this.saving = false;
        this.errorMessage = extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }

  // No renting id: the command writes about the client's most overdue hire (see
  // SendClientLateNoticeCommand). The outcome goes back to the list, which has
  // the banner for it — this panel may well be closed by then.
  remindLate() {
    if (!this.client?.id) return;

    this.lateNotice.confirmAndSend(this.name, this.client.id).subscribe(message => {
      if (message) {
        this.noticeMessage = message;
        this.changed = true;
      }
    });
  }

  /** Opens the reason box; nothing is erased until it is confirmed. */
  startErasing() {
    this.erasing = true;
    this.eraseReason = '';
  }

  cancelErasing() {
    this.erasing = false;
    this.eraseReason = '';
  }

  // Irreversible, and it deletes the identity scans from storage — so it asks
  // twice: the reason box, then the confirm. The archive action beside it is the
  // reversible one, and the copy is what tells them apart.
  eraseClient() {
    if (!this.client?.id || this.saving) return;

    if (!confirm(this.transloco.translate('client.confirmErase', { name: this.name }))) return;

    this.saving = true;
    this.errorMessage = '';

    const reason = this.eraseReason.trim();

    this.clients.eraseClientPersonalData(
      this.client.id,
      new EraseClientPersonalDataCommand({ id: this.client.id, reason: reason || undefined })
    ).subscribe({
      next: () => {
        this.saving = false;
        this.erasing = false;
        this.eraseReason = '';
        this.changed = true;
        // Re-read rather than close: what is left of the record is the answer,
        // and seeing it is how the operator knows the erasure landed.
        this.reload();
      },
      error: err => {
        this.saving = false;
        this.errorMessage = extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }

  deleteClient() {
    if (!this.client?.id) return;

    if (!confirm(this.transloco.translate('client.confirmDelete', { name: this.name }))) return;

    this.saving = true;

    this.clients.deleteClient(this.client.id).subscribe({
      next: () => {
        this.saving = false;
        this.changed = true;
        this.close();
      },
      error: err => {
        this.saving = false;
        this.errorMessage = extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }

  close() {
    this.dialogRef.close({ changed: this.changed, noticeMessage: this.noticeMessage });
  }
}
