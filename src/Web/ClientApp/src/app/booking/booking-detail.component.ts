import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import {
  ClientDto, ClientsClient, ExtraServiceDto, ExtraServicesClient, MoneyDto,
  RentingDto, RentingState, RentingsClient,
  ReservationDto, ReservationStatus, ReservationsClient
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';
import { CancelDialogComponent } from '../shared/cancel-dialog.component';
import {
  extractProblemDetail, extractValidationErrors, isInvalidTransition
} from '../shared/form-utils';

/** Which of the two kinds of booking the panel was opened on. */
export type BookingKind = 'renting' | 'reservation';

export interface BookingDetailData {
  kind: BookingKind;
  id: number;
}

export interface BookingDetailResult {
  /** Something moved while the panel was open — the list behind it is stale. */
  changed: boolean;
  /** Conversion only: the hire the hold became, for the caller to open. */
  rentingId?: number;
}

/** One dot on the lifecycle strip at the top of the panel. */
export interface BookingStep {
  labelKey: string;
  done: boolean;
  current: boolean;
}

/**
 * A booking read in full, beside the list it was clicked in: who is renting,
 * which car and when, where it is in its lifecycle, what it charges and what is
 * left to collect — and the actions that move it on.
 *
 * One panel for both kinds. A hire and a hold are the same booking at two points
 * of its life (a hold becomes a hire; see ConvertReservationCommand), the
 * questions asked of them at the counter are the same ones, and answering them
 * in two differently-shaped screens made the pair harder to read than it is.
 * What genuinely differs — the lifecycle steps, the money lines, the actions —
 * is branched on {@link BookingDetailData.kind}; everything else is shared.
 *
 * A slide-over rather than a centred box, for the reason the car's quick edit is
 * one: the list behind it is the context for what is being read, and a dialog
 * over the middle of the page hides exactly the row that was clicked. The edge is
 * chosen by the caller so it follows the reading direction.
 *
 * The record is re-read on open rather than passed in — the same reason the
 * return dialog re-reads its hire. A row that has been sitting on a list carries
 * a stale concurrency token and a stale state, and both the actions here and the
 * figures they are decided from have to come from the server as it is now.
 */
@Component({
  selector: 'app-booking-detail',
  templateUrl: './booking-detail.component.html',
  styleUrls: ['./booking-detail.component.css']
})
export class BookingDetailComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  readonly data = inject<BookingDetailData>(MAT_DIALOG_DATA);

  renting?: RentingDto;
  reservation?: ReservationDto;

  /** The hirer's file, for the contact line and the papers on record. Absent
   *  without Client.Read, which is a permission a counter can lack. */
  client?: ClientDto;
  /** Billed separately from the hire's price (see RentingDto.Outstanding), so
   *  they get a section of their own rather than a line in the total. */
  extras: ExtraServiceDto[] = [];

  /**
   * Derived from the record once it lands, not read off a getter. Both are bound
   * with *ngFor, and a getter that builds a new array is a new array on every
   * change-detection pass — which makes Angular tear the nodes down and rebuild
   * them each tick, since it diffs the list by identity.
   */
  steps: BookingStep[] = [];
  papers: string[] = [];
  /** What is still owed; a hold's is worked out here (see {@link money}). */
  outstanding?: MoneyDto;

  loading = true;
  /** Set by every successful action; returned to the list so it reloads once. */
  private changed = false;
  errorMessage = '';

  canPay = false;
  canChangeState = false;
  private canReadClients = false;
  private canReadExtras = false;

  RentingState = RentingState;
  ReservationStatus = ReservationStatus;

  constructor(
    private rentings: RentingsClient,
    private reservations: ReservationsClient,
    private clients: ClientsClient,
    private extraServices: ExtraServicesClient,
    private auth: AuthService,
    private actions: BookingActionsService,
    private dialogRef: MatDialogRef<BookingDetailComponent, BookingDetailResult>
  ) { }

  ngOnInit() {
    // Escape and a click on the backdrop are the two ways out that do not go
    // through close(), and Material's own handling of them closes with no
    // result at all — which would throw away the fact that something was
    // confirmed, paid or returned in here, leaving the list behind the panel
    // showing the status the booking had before. The caller opens with
    // disableClose so both land here instead.
    this.dialogRef.backdropClick().subscribe(() => this.close());
    this.dialogRef.keydownEvents().subscribe(event => {
      if (event.key === 'Escape') this.close();
    });

    this.auth.currentUser$.subscribe(user => {
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
      this.canChangeState = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canReadClients = AuthService.canAccessModule(user, 'Clients', 'Client.Read');
      this.canReadExtras = AuthService.canAccessModule(user, 'ExtraServices', 'ExtraService.Read');
      this.load();
    });
  }

  get isRenting(): boolean {
    return this.data.kind === 'renting';
  }

  // --- Reading --------------------------------------------------------------

  private load() {
    this.loading = true;

    if (this.isRenting) {
      this.rentings.getRentingById(this.data.id).subscribe({
        next: renting => {
          this.renting = renting;
          this.loadAround(renting.clientId, renting.id);
        },
        error: err => this.failedToLoad(err)
      });
      return;
    }

    this.reservations.getReservationById(this.data.id).subscribe({
      next: reservation => {
        this.reservation = reservation;
        this.loadAround(reservation.clientId, undefined);
      },
      error: err => this.failedToLoad(err)
    });
  }

  /**
   * The two reads that hang off the booking. Both are optional — either can be
   * refused by permission, and neither is worth failing the panel over — so they
   * answer with a null rather than an error, and the sections they feed simply
   * do not appear.
   */
  private loadAround(clientId: number | undefined, rentingId: number | undefined) {
    const client: Observable<ClientDto | null> = clientId && this.canReadClients
      ? this.clients.getClientById(clientId).pipe(catchError(() => of(null)))
      : of(null);

    const extras: Observable<ExtraServiceDto[] | null> = rentingId && this.canReadExtras
      ? this.extraServices.getExtraServicesByRenting(rentingId).pipe(catchError(() => of(null)))
      : of(null);

    forkJoin({ client, extras }).subscribe(result => {
      this.client = result.client ?? undefined;
      this.extras = result.extras ?? [];
      this.steps = this.buildSteps();
      this.papers = this.buildPapers();
      this.outstanding = this.money();
      this.loading = false;
    });
  }

  private failedToLoad(err: any) {
    this.loading = false;
    console.error(err);
    this.errorMessage = this.transloco.translate('common.unexpectedError');
  }

  /**
   * After an action: the record on screen is no longer what the server holds.
   *
   * The banner is NOT cleared here. A stale-state refusal both reports and
   * reloads (see fail and BookingActionsService), and clearing on the way
   * through would swallow the one message that explains why the panel just
   * changed under the user. Each action clears it on the way in instead.
   */
  private reload() {
    this.changed = true;
    this.load();
  }

  // --- Lifecycle ------------------------------------------------------------

  /**
   * The happy path as dots, with the booking's own position on it. A booking
   * that left the path — cancelled, rejected, expired — has no position on it,
   * so the strip is replaced by {@link terminalLabelKey} and its reason.
   */
  private buildSteps(): BookingStep[] {
    if (this.isTerminal) return [];

    // Both enums are numbered in declaration order, not in lifecycle order
    // (RentingState is Done=0, InProgress=1, NotYet=2), so how far along a
    // booking is has to be ranked here rather than compared with `>`.
    if (this.isRenting) {
      const rank = this.rentingRank(this.renting?.rentingState ?? RentingState.NotYet);
      return [
        { labelKey: 'enums.rentingState.notYet', done: rank > 0, current: rank === 0 },
        { labelKey: 'enums.rentingState.inProgress', done: rank > 1, current: rank === 1 },
        { labelKey: 'enums.rentingState.done', done: false, current: rank === 2 }
      ];
    }

    // Paid is a step of its own rather than a flavour of Confirmed: a hold can be
    // converted from either, but only one of them has had money taken against it.
    const status = this.reservation?.status ?? ReservationStatus.PendingConfirmation;
    const rank = this.reservationRank(status);
    return [
      { labelKey: 'enums.reservationStatus.pendingConfirmation', done: rank > 0, current: rank === 0 },
      { labelKey: 'enums.reservationStatus.confirmed', done: rank > 1, current: rank === 1 },
      { labelKey: 'enums.reservationStatus.paid', done: rank > 2, current: rank === 2 },
      { labelKey: 'enums.reservationStatus.converted', done: false, current: rank === 3 }
    ];
  }

  private rentingRank(state: RentingState): number {
    switch (state) {
      case RentingState.InProgress: return 1;
      case RentingState.Done: return 2;
      default: return 0;
    }
  }

  private reservationRank(status: ReservationStatus): number {
    switch (status) {
      case ReservationStatus.Confirmed: return 1;
      case ReservationStatus.Paid: return 2;
      case ReservationStatus.Converted: return 3;
      default: return 0;
    }
  }

  /** Cancelled, rejected or expired: off the happy path for good. */
  get isTerminal(): boolean {
    if (this.isRenting) return this.renting?.rentingState === RentingState.Cancelled;

    const status = this.reservation?.status;
    return status === ReservationStatus.Rejected
      || status === ReservationStatus.Cancelled
      || status === ReservationStatus.Expired;
  }

  get terminalLabelKey(): string {
    if (this.isRenting) return 'enums.rentingState.cancelled';

    switch (this.reservation?.status) {
      case ReservationStatus.Rejected: return 'enums.reservationStatus.rejected';
      case ReservationStatus.Expired: return 'enums.reservationStatus.expired';
      default: return 'enums.reservationStatus.cancelled';
    }
  }

  /** Why it left the path, in the words whoever ended it wrote. */
  get terminalReason(): string {
    const r = this.reservation;
    return r ? (r.rejectedReason || r.cancelledReason || r.expiredReason || '') : '';
  }

  get statusLabelKey(): string {
    if (this.isTerminal) return this.terminalLabelKey;
    return this.steps.find(step => step.current)?.labelKey ?? '';
  }

  // --- The booking's facts, whichever kind it is ----------------------------

  get title(): string {
    return `#${this.data.id}`;
  }

  get clientName(): string | undefined {
    return this.renting?.clientName ?? this.reservation?.clientName;
  }

  get clientId(): number | undefined {
    return this.renting?.clientId ?? this.reservation?.clientId;
  }

  get carId(): number | undefined {
    return this.renting?.carId ?? this.reservation?.carId;
  }

  get carModelName(): string | undefined {
    return this.renting?.carModelName ?? this.reservation?.carModelName;
  }

  get carMatricule(): string | undefined {
    return this.renting?.carMatricule ?? this.reservation?.carMatricule;
  }

  get startDate(): Date | undefined {
    return this.renting?.startDate ?? this.reservation?.startDate;
  }

  get endDate(): Date | undefined {
    return this.renting?.endDate ?? this.reservation?.endDate;
  }

  get notes(): string | undefined {
    return this.renting?.notes ?? this.reservation?.notes;
  }

  /** The identity papers on file, as the counter checks them off. */
  private buildPapers(): string[] {
    const c = this.client;
    if (!c) return [];

    const papers: string[] = [];
    if (c.cin) papers.push('client.cin');
    if (c.passeportNumber) papers.push('client.passeportNumber');
    if (c.drivingLicenceNumber) papers.push('client.drivingLicence');
    return papers;
  }

  // --- Money ----------------------------------------------------------------

  get price(): MoneyDto | undefined {
    return this.renting?.price ?? this.reservation?.price;
  }

  get paid(): MoneyDto | undefined {
    return this.renting?.paid ?? this.reservation?.payedPrice;
  }

  /**
   * What is still owed. A hire carries it (the server works it out over the
   * cancellation rule, see RentingDto), a hold does not — but a hold's charge is
   * simply its price, so the subtraction is safe to do here.
   */
  private money(): MoneyDto | undefined {
    if (this.isRenting) return this.renting?.outstanding;

    const r = this.reservation;
    if (!r?.price) return undefined;

    return new MoneyDto({
      amount: (r.price.amount ?? 0) - (r.payedPrice?.amount ?? 0),
      currency: r.price.currency
    });
  }

  /** Negative outstanding: the client has paid more than is charged. */
  get overpaid(): boolean {
    return (this.outstanding?.amount ?? 0) < 0;
  }

  get extrasTotal(): number {
    return this.extras.reduce((sum, extra) => sum + (extra.totalAmount?.amount ?? 0), 0);
  }

  get extrasCurrency(): string | undefined {
    return this.extras.find(extra => extra.totalAmount?.currency)?.totalAmount?.currency
      ?? this.price?.currency;
  }

  // --- Actions --------------------------------------------------------------

  get canPayNow(): boolean {
    if (!this.canPay) return false;
    if (this.isRenting) return (this.renting?.outstanding?.amount ?? 0) > 0;
    return this.isConvertible && (this.outstanding?.amount ?? 0) > 0;
  }

  pay() {
    this.errorMessage = '';
    const outstanding = this.outstanding;

    this.dialog.open(PaymentDialogComponent, {
      data: {
        target: { kind: this.data.kind, id: this.data.id },
        subtitle: [this.clientName, this.carMatricule].filter(Boolean).join(' — '),
        outstanding: outstanding?.amount,
        currency: outstanding?.currency ?? this.price?.currency
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(recorded => {
      if (recorded) this.reload();
    });
  }

  // --- Actions: rentings ----------------------------------------------------

  get canStart(): boolean {
    return this.canChangeState && this.renting?.rentingState === RentingState.NotYet;
  }

  start() {
    if (this.renting) this.apply(this.actions.startRenting(this.renting));
  }

  get canTakeBack(): boolean {
    return this.canChangeState && this.renting?.rentingState === RentingState.InProgress;
  }

  takeBack() {
    if (this.renting) this.apply(this.actions.returnRenting(this.renting));
  }

  get canCancelRenting(): boolean {
    return this.renting?.rentingState === RentingState.NotYet
      || this.renting?.rentingState === RentingState.InProgress;
  }

  // Cancelling decides what happens to the money as well as to the booking, so it
  // opens the dialog that asks (see CancelDialogComponent) rather than a yes/no box.
  cancelRenting() {
    const renting = this.renting;
    if (!renting?.id) return;

    this.errorMessage = '';

    this.dialog.open(CancelDialogComponent, {
      data: {
        rentingId: renting.id,
        carLabel: [renting.carMatricule, renting.carModelName].filter(Boolean).join(' · '),
        clientName: renting.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(cancelled => {
      if (cancelled) this.reload();
    });
  }

  // --- Actions: reservations ------------------------------------------------

  get isPending(): boolean {
    return this.reservation?.status === ReservationStatus.PendingConfirmation;
  }

  get isConvertible(): boolean {
    return this.reservation?.status === ReservationStatus.Confirmed
      || this.reservation?.status === ReservationStatus.Paid;
  }

  get isActiveHold(): boolean {
    return this.isPending || this.isConvertible;
  }

  confirm() {
    if (this.reservation) this.apply(this.actions.confirmReservation(this.reservation));
  }

  reject() {
    if (this.reservation) this.apply(this.actions.rejectReservation(this.reservation));
  }

  /** The hold becomes a hire; the caller opens it, so the panel closes on it. */
  convert() {
    if (!this.reservation) return;

    this.errorMessage = '';

    this.actions.convertReservation(this.reservation).subscribe(outcome => {
      if (outcome.rentingId) {
        this.dialogRef.close({ changed: true, rentingId: outcome.rentingId });
        return;
      }

      this.handle(outcome);
    });
  }

  cancelReservation() {
    const reservation = this.reservation;
    if (!reservation?.id) return;

    const reason = prompt(this.transloco.translate('reservation.promptCancelReason')) ?? undefined;
    if (reason === undefined && !confirm(this.transloco.translate('reservation.confirmCancel'))) return;

    this.reservations.cancelReservation(reservation.id, reason).subscribe({
      next: () => this.reload(),
      error: err => this.fail(err)
    });
  }

  // --- Outcomes -------------------------------------------------------------

  private apply(action: Observable<BookingActionOutcome>) {
    this.errorMessage = '';
    action.subscribe(outcome => this.handle(outcome));
  }

  private handle(outcome: BookingActionOutcome) {
    if (outcome.error) this.errorMessage = outcome.error;
    if (outcome.changed) this.reload();
  }

  private fail(err: any) {
    // The hold moved on while the panel was open (someone else confirmed or
    // cancelled it). What is on screen is stale, so say what happened and re-read
    // rather than leaving the old status and its now-wrong buttons up.
    if (isInvalidTransition(err)) {
      this.errorMessage = this.transloco.translate('reservation.staleState');
      this.reload();
      return;
    }

    const said = extractValidationErrors(err) ?? extractProblemDetail(err);
    if (!said) console.error(err);

    this.errorMessage = said ?? this.transloco.translate('common.actionFailed');
  }

  close() {
    this.dialogRef.close({ changed: this.changed });
  }
}
