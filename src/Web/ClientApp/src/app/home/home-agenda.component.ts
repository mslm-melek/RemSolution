import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../shared/auth.service';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import { LateNoticeService } from '../shared/late-notice.service';
import { toUtcDateInput } from '../shared/form-utils';
import {
  RentingDateBasis, RentingDto, RentingState, RentingsClient,
  ReservationDto, ReservationStatus, ReservationsClient
} from '../web-api-client';

/**
 * The desk's work queue on the home screen: the holds waiting for someone to say
 * yes, the cars that should already be back, and what today asks for. Three
 * questions the agency opens the app to answer, each answered with the rows
 * themselves and the button that deals with them — so approving a request or
 * taking a car back never needs the list screen at all.
 *
 * Each queue is its own query, gated by the module it reads, so a user who may
 * see hires but not holds gets the two renting queues and no third. The actions
 * come from BookingActionsService — the same ones the bookings lists put on their
 * rows, prompts and failure wording included.
 *
 * Dates: the API's are wall-clock values stamped UTC (see form-utils), so "today"
 * is the browser's calendar day sent as UTC midnight — exactly what the booking
 * calendar does, and what makes a car booked out on the 5th belong to the 5th
 * whatever the offset.
 */

/** What a queue's figure is, for the home screen's own tiles to reuse. */
export interface AgendaCounts {
  /** Holds awaiting the agency: the "Requests to confirm" tile's number. */
  pendingReservations?: number;
}

/** One thing today asks for. Rentings and holds land in one list, by time. */
export interface TodayRow {
  kind: 'pickup' | 'return' | 'request';
  /** The moment it is due — a pickup's start, a return's end, a hold's start. */
  when?: Date;
  /** Exactly one of the two is set, following `kind`. */
  renting?: RentingDto;
  reservation?: ReservationDto;
}

// Rows per queue. A queue is a peek at the top of a list, not the list: what does
// not fit is reached through the "see all" link, which carries the same filter.
const QUEUE_ROWS = 4;

@Component({
  selector: 'app-home-agenda',
  templateUrl: './home-agenda.component.html',
  styleUrls: ['./home-agenda.component.css']
})
export class HomeAgendaComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly reservationsClient = inject(ReservationsClient);
  private readonly rentingsClient = inject(RentingsClient);
  private readonly actions = inject(BookingActionsService);
  private readonly lateNotice = inject(LateNoticeService);
  private readonly router = inject(Router);

  /**
   * The figures this component already asked the server for. The home screen's
   * "Requests to confirm" tile counts exactly what the first queue counts, so it
   * takes the number from here rather than repeating the query.
   */
  @Output() counts = new EventEmitter<AgendaCounts>();

  // --- The three queues -----------------------------------------------------

  /** Holds awaiting the agency, soonest pickup first. */
  pending: ReservationDto[] = [];
  pendingTotal = 0;

  /** Hires still out that were due back before today, longest overdue first. */
  overdue: RentingDto[] = [];
  overdueTotal = 0;

  /** Today's pickups, returns and holds in one list, by the hour they are due. */
  today: TodayRow[] = [];
  todayPickups = 0;
  todayReturns = 0;
  todayRequests = 0;

  loading = 0;
  /** Shown once, above the queues: an action's refusal, or a late notice's outcome. */
  message = '';

  // What this user may see and do. A queue whose module is off or unreadable is
  // not rendered at all; an action the user lacks the write permission for is
  // left off its row, which still links to the record.
  canReadReservations = false;
  canReadRentings = false;
  canActOnReservations = false;
  canActOnRentings = false;
  canRemind = false;

  readonly ReservationStatus = ReservationStatus;

  // The window every "today" query and link is built from: [today, tomorrow).
  // `today0` is public because the heading names the day — read with the UTC
  // parts (`date:'…':'UTC'`), since that is where its calendar day lives.
  readonly today0 = startOfToday();
  private readonly tomorrow0 = addDays(this.today0, 1);

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canReadReservations =
        AuthService.canAccessModule(user, 'Reservations', 'Reservation.Read');
      this.canActOnReservations =
        AuthService.canAccessModule(user, 'Reservations', 'Reservation.Update');
      this.canReadRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canActOnRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canRemind = AuthService.canAccessModule(user, 'Notifications', 'Notification.Send');

      this.load();
    });
  }

  /** Whether there is any queue to draw at all (see the permissions above). */
  get isVisible(): boolean {
    return this.canReadReservations || this.canReadRentings;
  }

  /** Nothing anywhere — said once, rather than as three empty cards. */
  get isAllClear(): boolean {
    return !this.loading && !this.pendingTotal && !this.overdueTotal && !this.today.length;
  }

  // --- Loading --------------------------------------------------------------

  private load() {
    if (this.canReadReservations) {
      this.loadPending();
      this.loadTodayRequests();
    }

    if (this.canReadRentings) {
      this.loadOverdue();
      this.loadTodayRentings(RentingState.NotYet, RentingDateBasis.Starts, 'pickup');
      this.loadTodayRentings(RentingState.InProgress, RentingDateBasis.Ends, 'return');
    }
  }

  /** Every queue again, after an action moved something. */
  reload() {
    this.pending = [];
    this.overdue = [];
    this.today = [];
    this.pendingTotal = 0;
    this.overdueTotal = 0;
    this.todayPickups = 0;
    this.todayReturns = 0;
    this.todayRequests = 0;
    this.load();
  }

  private loadPending() {
    // Soonest pickup first: the request for tomorrow is the one that has to be
    // answered today, whatever order the requests arrived in.
    this.track(
      this.reservationsClient.getReservations(
        1, QUEUE_ROWS, null, null, ReservationStatus.PendingConfirmation,
        null, null, false, 'period', false),
      result => {
        this.pending = result.items ?? [];
        this.pendingTotal = result.totalCount ?? 0;
        this.counts.emit({ pendingReservations: this.pendingTotal });
      });
  }

  private loadOverdue() {
    // Due back strictly before today. A hire due later today is not late yet — it
    // is in the "today" queue, which is where the desk should read it from.
    this.track(
      this.rentingsClient.getRentings(
        1, QUEUE_ROWS, null, null, RentingState.InProgress,
        null, this.today0, RentingDateBasis.Ends, false, 'enddate', false),
      result => {
        this.overdue = result.items ?? [];
        this.overdueTotal = result.totalCount ?? 0;
      });
  }

  private loadTodayRentings(
    state: RentingState, basis: RentingDateBasis, kind: 'pickup' | 'return') {
    this.track(
      this.rentingsClient.getRentings(
        1, QUEUE_ROWS, null, null, state,
        this.today0, this.tomorrow0, basis, false,
        basis === RentingDateBasis.Ends ? 'enddate' : 'period', false),
      result => {
        const total = result.totalCount ?? 0;
        if (kind === 'pickup') { this.todayPickups = total; } else { this.todayReturns = total; }

        this.addToday((result.items ?? []).map(renting => ({
          kind,
          when: kind === 'pickup' ? renting.startDate : renting.endDate,
          renting
        })));
      });
  }

  private loadTodayRequests() {
    // Holds starting today that are still in play — a rejected or already
    // converted one is a row, not a job (see the query's ActiveOnly).
    this.track(
      this.reservationsClient.getReservations(
        1, QUEUE_ROWS, null, null, null,
        this.today0, this.tomorrow0, true, 'period', false),
      result => {
        this.todayRequests = result.totalCount ?? 0;

        this.addToday((result.items ?? []).map(reservation => ({
          kind: 'request' as const,
          when: reservation.startDate,
          reservation
        })));
      });
  }

  // Three queries fill one list, so it is re-sorted as each answers — and kept to
  // the same depth as a single-source queue, or a busy morning of pickups would
  // push the day's returns off the card.
  private addToday(rows: TodayRow[]) {
    this.today = [...this.today, ...rows]
      .sort((a, b) => (a.when?.getTime() ?? 0) - (b.when?.getTime() ?? 0))
      .slice(0, QUEUE_ROWS * 2);
  }

  // A count rather than a flag: the queues load in parallel, and the bar stays up
  // until the last of them has answered. A failed query leaves its queue empty and
  // says nothing — the landing page is not the place for a banner about one card
  // (the lists themselves report their own failures).
  private track<T>(request: Observable<T>, apply: (value: T) => void) {
    this.loading++;

    request.subscribe({
      next: value => {
        this.loading--;
        apply(value);
      },
      error: err => {
        this.loading--;
        console.error(err);
      }
    });
  }

  // --- Acting ---------------------------------------------------------------

  confirm(reservation: ReservationDto) {
    this.apply(this.actions.confirmReservation(reservation));
  }

  reject(reservation: ReservationDto) {
    this.apply(this.actions.rejectReservation(reservation));
  }

  convert(reservation: ReservationDto) {
    this.actions.convertReservation(reservation).subscribe(outcome => {
      // Straight into the hire it became: converting is the start of handing the
      // car over, and the renting page is where the rest of it happens.
      if (outcome.rentingId) {
        this.router.navigate(['/renting', outcome.rentingId]);
        return;
      }

      this.handle(outcome);
    });
  }

  start(renting: RentingDto) {
    this.apply(this.actions.startRenting(renting));
  }

  takeBack(renting: RentingDto) {
    this.apply(this.actions.returnRenting(renting));
  }

  /** Tells the client their car is overdue. Asks first — this writes to a customer. */
  remind(renting: RentingDto) {
    if (!renting.clientId) return;

    this.lateNotice
      .confirmAndSend(renting.clientName ?? '', renting.clientId, renting.id)
      .subscribe(message => {
        if (message) this.show(message);
      });
  }

  private apply(action: Observable<BookingActionOutcome>) {
    action.subscribe(outcome => this.handle(outcome));
  }

  private handle(outcome: BookingActionOutcome) {
    if (outcome.error) this.show(outcome.error);
    if (outcome.changed) this.reload();
  }

  private show(message: string) {
    this.message = message;
    setTimeout(() => this.message = '', 6000);
  }

  // --- Presentation ---------------------------------------------------------

  /** The car, however much of it the row knows — the calendar's reading. */
  carLabel(row: RentingDto | ReservationDto): string {
    return row.carMatricule || row.carModelName || '';
  }

  /** Whole days between the hire's return date and today, at least one. */
  daysOverdue(renting: RentingDto): number {
    if (!renting.endDate) return 0;

    const due = Date.UTC(
      renting.endDate.getUTCFullYear(), renting.endDate.getUTCMonth(), renting.endDate.getUTCDate());
    const days = Math.floor((this.today0.getTime() - due) / MS_PER_DAY);

    return Math.max(days, 1);
  }

  /** A pending hold that lapses today, which the desk should answer first. */
  isExpiringToday(reservation: ReservationDto): boolean {
    return !!reservation.expiresAt && reservation.expiresAt < this.tomorrow0;
  }

  /** What a hold's own status is called — the reservation list's wording, reused. */
  statusLabelKey(status?: ReservationStatus): string {
    return status === undefined ? '' : RESERVATION_STATUS_KEYS[status] ?? '';
  }

  /** Chip tone, matching the bookings lists: awaiting the agency reads as a warning. */
  statusClass(status?: ReservationStatus): string {
    return status === ReservationStatus.PendingConfirmation ? 'warn' : 'info';
  }

  icon(kind: TodayRow['kind']): string {
    switch (kind) {
      case 'pickup': return 'logout';
      case 'return': return 'login';
      default: return 'event_available';
    }
  }

  /** The calendar's words for the three kinds, reused rather than re-invented. */
  kindLabelKey(kind: TodayRow['kind']): string {
    switch (kind) {
      case 'pickup': return 'calendar.pickup';
      case 'return': return 'calendar.return';
      default: return 'calendar.request';
    }
  }

  // --- Where each queue's "see all" goes ------------------------------------
  // Every link carries the filter its figure was counted with, so the list opens
  // showing exactly the rows the card showed (see shared/list-filters).

  readonly rentingLink = ['/renting'];
  readonly reservationLink = ['/reservation'];

  readonly pendingParams = { status: 'PendingConfirmation' };

  get overdueParams() {
    return { state: 'InProgress', dateBasis: 'Ends', to: toUtcDateInput(this.today0) };
  }

  get todayPickupParams() {
    return { state: 'NotYet', dateBasis: 'Starts', ...this.todayWindow };
  }

  get todayReturnParams() {
    return { state: 'InProgress', dateBasis: 'Ends', ...this.todayWindow };
  }

  get todayRequestParams() {
    return { active: 'true', ...this.todayWindow };
  }

  private get todayWindow(): { from: string; to: string } {
    // The dates as the lists read them (`yyyy-MM-dd`, parsed back to the UTC
    // midnights these queries were sent with — hence the UTC parts, not the local
    // ones, which are a day out in a negative offset).
    return { from: toUtcDateInput(this.today0), to: toUtcDateInput(this.tomorrow0) };
  }
}

// The enum → transloco key map the reservation list uses, so a hold is called the
// same thing here as it is there.
const RESERVATION_STATUS_KEYS: Record<ReservationStatus, string> = {
  [ReservationStatus.PendingConfirmation]: 'enums.reservationStatus.pendingConfirmation',
  [ReservationStatus.Confirmed]: 'enums.reservationStatus.confirmed',
  [ReservationStatus.Paid]: 'enums.reservationStatus.paid',
  [ReservationStatus.Converted]: 'enums.reservationStatus.converted',
  [ReservationStatus.Rejected]: 'enums.reservationStatus.rejected',
  [ReservationStatus.Cancelled]: 'enums.reservationStatus.cancelled',
  [ReservationStatus.Expired]: 'enums.reservationStatus.expired'
};

const MS_PER_DAY = 24 * 60 * 60 * 1000;

/**
 * The browser's calendar day as UTC midnight — the instant the API reads a date
 * as (see form-utils' fromDateInput) and the one the lists' `?from=`/`?to=`
 * params parse back to.
 */
function startOfToday(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
}

function addDays(date: Date, days: number): Date {
  return new Date(date.getTime() + days * MS_PER_DAY);
}
