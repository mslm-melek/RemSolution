import { Component, Input, OnChanges, OnInit, SimpleChanges, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../shared/auth.service';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import { LateNoticeService } from '../shared/late-notice.service';

import {
  BookingCalendarEventDto, BookingCalendarEventKind, DashboardClient,
  RentingDto, RentingsClient, ReservationDto, ReservationStatus
} from '../web-api-client';
import { wallClockNow } from '../shared/form-utils';

/**
 * The agenda on the landing screen: what leaves, what comes back, and which holds
 * start — by day, by week or by month.
 *
 * One source for all three views (GET /api/Dashboard/calendar), because they are
 * three readings of the same question and fetching them separately would let the
 * month disagree with the day inside it. The window is what changes: a day, the
 * seven days of its week, or the whole month grid.
 *
 * The day view is where the desk works, so its rows carry the action itself —
 * handing the car over, taking it back, saying yes to a hold — through
 * BookingActionsService, the same actions the bookings lists put on their rows.
 * The week and month views are for looking ahead, so they navigate instead.
 *
 * Dates: the API's are wall-clock values stamped UTC (see form-utils), so a day
 * is a calendar day read with the UTC parts and sent as UTC midnight — which is
 * what makes a car booked out on the 5th belong to the 5th whatever the offset.
 */

export type AgendaView = 'day' | 'week' | 'month';

/** One day's column in the week grid, or one cell of the month grid. */
export interface AgendaDay {
  /** UTC midnight of the day. */
  on: Date;
  /** Outside the month being drawn — a leading or trailing grid cell. */
  padding: boolean;
  isToday: boolean;
  events: BookingCalendarEventDto[];
  out: number;
  back: number;
}

const MS_PER_DAY = 24 * 60 * 60 * 1000;

@Component({
  selector: 'app-home-agenda',
  templateUrl: './home-agenda.component.html',
  styleUrls: ['./home-agenda.component.css']
})
export class HomeAgendaComponent implements OnInit, OnChanges {
  private readonly auth = inject(AuthService);
  private readonly dashboard = inject(DashboardClient);
  private readonly rentingsClient = inject(RentingsClient);
  private readonly actions = inject(BookingActionsService);
  private readonly lateNotice = inject(LateNoticeService);
  private readonly router = inject(Router);

  /** The branch the whole screen is scoped to, or null for the whole agency. */
  @Input() branchId: number | null = null;

  view: AgendaView = 'day';
  /** Days, weeks or months from today, following `view`. */
  offset = 0;

  loading = false;
  /** An action's refusal, or what a late notice actually did. Clears itself. */
  message = '';

  /** The window on screen, and what fell inside it. */
  events: BookingCalendarEventDto[] = [];
  days: AgendaDay[] = [];
  from = startOfToday();
  to = addDays(startOfToday(), 1);
  truncated = false;

  // What this user may do. What they may SEE is decided by the server, which
  // omits the half of the calendar their modules do not cover.
  canActOnReservations = false;
  canActOnRentings = false;
  canRemind = false;

  readonly today0 = startOfToday();
  readonly Kind = BookingCalendarEventKind;
  readonly ReservationStatus = ReservationStatus;

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canActOnReservations =
        AuthService.canAccessModule(user, 'Reservations', 'Reservation.Update');
      this.canActOnRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canRemind = AuthService.canAccessModule(user, 'Notifications', 'Notification.Send');
    });

    this.load();
  }

  ngOnChanges(changes: SimpleChanges) {
    // The branch picker moved: the agenda is scoped by it like everything else on
    // the screen. Skipped on the first pass, which ngOnInit already loads.
    if (changes['branchId'] && !changes['branchId'].firstChange) this.load();
  }

  // --- The window -----------------------------------------------------------

  setView(view: AgendaView) {
    if (view === this.view) return;

    // Back to today rather than "the same offset in the new unit": three months
    // ahead is not where somebody switching from day to month meant to land.
    this.view = view;
    this.offset = 0;
    this.load();
  }

  shift(step: number) {
    this.offset += step;
    this.load();
  }

  today() {
    if (this.offset === 0) return;
    this.offset = 0;
    this.load();
  }

  /** A month cell opens that day, which is where the actions are. */
  openDay(day: AgendaDay) {
    this.view = 'day';
    this.offset = Math.round((day.on.getTime() - this.today0.getTime()) / MS_PER_DAY);
    this.load();
  }

  get isToday(): boolean {
    return this.view === 'day' && this.offset === 0;
  }

  // --- Loading --------------------------------------------------------------

  reload() {
    this.load();
  }

  private load() {
    const [from, to] = this.window();
    this.from = from;
    this.to = to;
    this.loading = true;

    this.dashboard.getBookingCalendar(from, to, this.branchId).subscribe({
      next: result => {
        this.loading = false;
        this.events = result.events ?? [];
        this.truncated = result.isTruncated === true;
        this.days = this.buildDays(from, to);
      },
      // The landing page is not the place for a banner about one panel; the lists
      // it links to report their own failures.
      error: err => {
        this.loading = false;
        this.events = [];
        this.days = this.buildDays(from, to);
        console.error(err);
      }
    });
  }

  /** Half-open [from, to) for the current view and offset. */
  private window(): [Date, Date] {
    if (this.view === 'day') {
      const day = addDays(this.today0, this.offset);
      return [day, addDays(day, 1)];
    }

    if (this.view === 'week') {
      const monday = addDays(startOfWeek(this.today0), this.offset * 7);
      return [monday, addDays(monday, 7)];
    }

    // The whole month GRID, adjacent-month days included, so the leading and
    // trailing cells are populated rather than blank.
    const first = startOfMonth(this.today0, this.offset);
    const gridStart = startOfWeek(first);
    const next = startOfMonth(this.today0, this.offset + 1);
    const gridEnd = addDays(startOfWeek(addDays(next, -1)), 7);

    return [gridStart, gridEnd];
  }

  /**
   * The month being drawn. Named by the heading, and what decides which grid
   * cells are the previous or next month's padding — the window itself starts on
   * a Monday that is often in the month before.
   */
  get monthShown(): Date {
    return startOfMonth(this.today0, this.offset);
  }

  private buildDays(from: Date, to: Date): AgendaDay[] {
    if (this.view === 'day') return [];

    const month = this.monthShown;
    const days: AgendaDay[] = [];

    for (let on = from; on < to; on = addDays(on, 1)) {
      const events = this.eventsOn(on);

      days.push({
        on,
        padding: this.view === 'month' && on.getUTCMonth() !== month.getUTCMonth(),
        isToday: on.getTime() === this.today0.getTime(),
        events,
        out: events.filter(e => this.isOut(e)).length,
        back: events.filter(e => e.kind === BookingCalendarEventKind.Return).length,
      });
    }

    return days;
  }

  /** The day's entries, in the order they fall due. */
  eventsOn(day: Date): BookingCalendarEventDto[] {
    return this.events.filter(e => e.on && sameUtcDay(e.on, day));
  }

  /** The day view's rows: the whole window, which is one day. */
  get rows(): BookingCalendarEventDto[] {
    return this.events;
  }

  // --- Reading an entry -----------------------------------------------------

  /** A car leaving: a hire's pickup, or a hold that starts. */
  isOut(event: BookingCalendarEventDto): boolean {
    return event.kind !== BookingCalendarEventKind.Return;
  }

  /** The car, however much of it the entry knows. */
  carLabel(event: BookingCalendarEventDto): string {
    return event.carMatricule || event.carModelName || '';
  }

  kindLabelKey(event: BookingCalendarEventDto): string {
    switch (event.kind) {
      case BookingCalendarEventKind.Pickup: return 'calendar.pickup';
      case BookingCalendarEventKind.Return: return 'calendar.return';
      default: return 'calendar.request';
    }
  }

  /** The first name alone — a week column is 100px wide. */
  shortName(event: BookingCalendarEventDto): string {
    return (event.clientName ?? '').trim().split(/\s+/)[0] ?? '';
  }

  /**
   * Its hour has gone by, so it stops competing with what is still to do.
   * Compared against the wall clock rebuilt as a UTC instant, not against
   * Date.now(): the API's times are wall-clock values stamped UTC, and comparing
   * the two directly is wrong by the browser's offset — a whole morning of rows
   * would grey out at once in Casablanca in summer.
   */
  isPast(event: BookingCalendarEventDto): boolean {
    return !event.isLate && !!event.on && event.on.getTime() < wallClockNow();
  }

  /** Whether this row still has something the desk does to it. */
  canAct(event: BookingCalendarEventDto): boolean {
    if (event.rentingId) return this.canActOnRentings;
    return this.canActOnReservations;
  }

  // --- Acting ---------------------------------------------------------------

  /**
   * Hands the car over. The hire is re-read first: the odometer the prompt offers
   * and the row version it writes with are not on a calendar entry, and acting on
   * a figure the panel has been holding since page load is how a stale reading
   * gets recorded as a real one.
   */
  start(event: BookingCalendarEventDto) {
    if (!event.rentingId) return;

    this.rentingsClient.getRentingById(event.rentingId).subscribe({
      next: renting => this.apply(this.actions.startRenting(renting)),
      error: err => {
        console.error(err);
        this.reload();
      }
    });
  }

  /** Takes the car back. The dialog re-reads the hire itself, so the id is enough. */
  takeBack(event: BookingCalendarEventDto) {
    if (!event.rentingId) return;

    this.apply(this.actions.returnRenting(new RentingDto({
      id: event.rentingId,
      carMatricule: event.carMatricule,
      carModelName: event.carModelName,
      clientName: event.clientName,
    })));
  }

  confirm(event: BookingCalendarEventDto) {
    if (!event.reservationId) return;
    this.apply(this.actions.confirmReservation(new ReservationDto({ id: event.reservationId })));
  }

  reject(event: BookingCalendarEventDto) {
    if (!event.reservationId) return;
    this.apply(this.actions.rejectReservation(new ReservationDto({ id: event.reservationId })));
  }

  convert(event: BookingCalendarEventDto) {
    if (!event.reservationId) return;

    this.actions.convertReservation(new ReservationDto({ id: event.reservationId }))
      .subscribe(outcome => {
        // Straight into the hire it became: converting is the start of handing the
        // car over, and the renting page is where the rest of it happens.
        if (outcome.rentingId) {
          this.router.navigate(['/renting', outcome.rentingId]);
          return;
        }

        this.handle(outcome);
      });
  }

  /** Tells the client their car is overdue. Asks first — this writes to a customer. */
  remind(event: BookingCalendarEventDto) {
    if (!event.clientId || !event.rentingId) return;

    this.lateNotice
      .confirmAndSend(event.clientName ?? '', event.clientId, event.rentingId)
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

  // --- Where an entry leads -------------------------------------------------

  link(event: BookingCalendarEventDto): unknown[] {
    return event.rentingId ? ['/renting', event.rentingId] : ['/reservation', event.reservationId];
  }
}

// --- The calendar arithmetic, all in UTC parts -------------------------------

/**
 * The browser's calendar day as UTC midnight — the instant the API reads a date
 * as (see form-utils' fromDateInput).
 */
function startOfToday(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
}

function addDays(date: Date, days: number): Date {
  return new Date(date.getTime() + days * MS_PER_DAY);
}

/** Monday of the week a day falls in. */
function startOfWeek(date: Date): Date {
  return addDays(date, -((date.getUTCDay() + 6) % 7));
}

function startOfMonth(anchor: Date, monthsAhead: number): Date {
  return new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth() + monthsAhead, 1));
}

function sameUtcDay(a: Date, b: Date): boolean {
  return a.getUTCFullYear() === b.getUTCFullYear()
      && a.getUTCMonth() === b.getUTCMonth()
      && a.getUTCDate() === b.getUTCDate();
}
