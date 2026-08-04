import { Component, OnInit } from '@angular/core';
import { AuthService } from './auth.service';
import { toDateInput } from './form-utils';
import {
  BookingCalendarEventDto, BookingCalendarEventKind, DashboardClient,
  RentingState, ReservationStatus
} from '../web-api-client';

/**
 * The agency's month: which cars go out on which day, which are due back, and
 * which holds are still waiting to become hires. Pinned to the home screen (see
 * home-widgets), and read by the desk to plan the day rather than to edit
 * anything — every entry links to the record it came from and nothing more.
 *
 * Days are the app's calendar days, not instants: the API serves its dates
 * offset-less and the generated client parses them as local midnight, so an
 * entry is filed under the local date parts of what came back — the same reading
 * `toDateInput` takes, which is why a booking shown on the 5th here is the one
 * the booking form calls the 5th.
 */

/** One cell of the grid. */
export interface CalendarDay {
  date: Date;
  /** `yyyy-MM-dd` — the bucket the entries are filed under, and the cell's id. */
  key: string;
  /** False for the leading and trailing cells borrowed from the months either side. */
  inMonth: boolean;
  isToday: boolean;
  events: BookingCalendarEventDto[];
  pickups: number;
  returns: number;
  requests: number;
  /** A car still out that was due back on or before this day. */
  hasLate: boolean;
}

// A month grid needs at most six weeks, and never fewer than four.
const DAYS_PER_WEEK = 7;

@Component({
  selector: 'app-booking-calendar',
  templateUrl: './booking-calendar.component.html',
  styleUrls: ['./booking-calendar.component.css']
})
export class BookingCalendarComponent implements OnInit {
  // Local midnight on the first of the month being shown.
  month = startOfMonth(new Date());

  weeks: CalendarDay[][] = [];
  /** The first week's dates, so the template can name the columns via the date pipe. */
  weekdays: Date[] = [];

  selected: CalendarDay | null = null;

  loading = false;
  loadFailed = false;
  /** The window held more bookings than one screen carries (see the query). */
  truncated = false;

  // Whether an entry may be opened. The calendar itself only needs the overview
  // permission, so a user can perfectly well see a hire here and have no business
  // opening it — those entries render as plain text instead of a link.
  canOpenRenting = false;
  canOpenReservation = false;

  readonly kinds = BookingCalendarEventKind;

  constructor(private auth: AuthService, private dashboard: DashboardClient) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canOpenRenting = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canOpenReservation = AuthService.canAccessModule(user, 'Reservations', 'Reservation.Read');
    });

    this.load();
  }

  // --- Navigation -----------------------------------------------------------

  previousMonth() {
    this.month = addMonths(this.month, -1);
    this.load();
  }

  nextMonth() {
    this.month = addMonths(this.month, 1);
    this.load();
  }

  goToToday() {
    const today = startOfMonth(new Date());
    if (today.getTime() === this.month.getTime()) return;

    this.month = today;
    this.load();
  }

  get isCurrentMonth(): boolean {
    return this.month.getTime() === startOfMonth(new Date()).getTime();
  }

  select(day: CalendarDay) {
    // Clicking the open day closes it: the grid is the point, the list is a peek.
    this.selected = this.selected?.key === day.key ? null : day;
  }

  // --- Loading --------------------------------------------------------------

  private load() {
    // The grid is built before the call so the month it shows always matches the
    // heading; the entries drop into its cells when they arrive.
    this.buildGrid();

    this.loading = true;
    this.loadFailed = false;

    const from = this.weeks[0][0].date;
    const lastWeek = this.weeks[this.weeks.length - 1];
    const to = addDays(lastWeek[DAYS_PER_WEEK - 1].date, 1);

    // UTC midnight, like every other date this app sends: the generated client
    // serializes with toISOString(), and the server's dates are the calendar
    // dates that reading gives back (see form-utils' fromDateInput).
    this.dashboard.getBookingCalendar(asUtcMidnight(from), asUtcMidnight(to)).subscribe({
      next: result => {
        this.loading = false;
        this.truncated = result.isTruncated === true;
        this.fill(result.events ?? []);
      },
      error: err => {
        this.loading = false;
        this.loadFailed = true;
        console.error(err);
      }
    });
  }

  // Whole weeks from the Monday on or before the 1st to the Sunday on or after
  // the last day. Monday-first everywhere, including Arabic: the agencies run a
  // Monday-to-Sunday week, and the grid mirrors itself for right-to-left anyway.
  private buildGrid() {
    const first = this.month;
    const last = addDays(addMonths(first, 1), -1);

    const start = addDays(first, -mondayIndex(first));
    const end = addDays(last, DAYS_PER_WEEK - 1 - mondayIndex(last));

    const todayKey = toDateInput(new Date());

    this.weeks = [];

    for (let cursor = start; cursor <= end; cursor = addDays(cursor, DAYS_PER_WEEK)) {
      const week: CalendarDay[] = [];

      for (let i = 0; i < DAYS_PER_WEEK; i++) {
        const date = addDays(cursor, i);
        const key = toDateInput(date);

        week.push({
          date,
          key,
          inMonth: date.getMonth() === first.getMonth(),
          isToday: key === todayKey,
          events: [],
          pickups: 0,
          returns: 0,
          requests: 0,
          hasLate: false
        });
      }

      this.weeks.push(week);
    }

    this.weekdays = this.weeks[0].map(day => day.date);
    this.selected = null;
  }

  private fill(events: BookingCalendarEventDto[]) {
    const byKey = new Map<string, CalendarDay>();
    for (const week of this.weeks) {
      for (const day of week) byKey.set(day.key, day);
    }

    for (const event of events) {
      if (!event.on) continue;

      // A day outside the grid can only mean the window and the grid disagree,
      // which they do not — but dropping it is better than losing the entry into
      // a cell nobody is looking at.
      const day = byKey.get(toDateInput(event.on));
      if (!day) continue;

      day.events.push(event);

      switch (event.kind) {
        case BookingCalendarEventKind.Pickup:
          day.pickups++;
          break;
        case BookingCalendarEventKind.Return:
          day.returns++;
          day.hasLate = day.hasLate || event.isLate === true;
          break;
        default:
          day.requests++;
          break;
      }
    }

    // Today's work is what the desk came for, so it opens by itself when today is
    // on screen; otherwise the month is browsed and nothing is presumed.
    this.selected = this.allDays().find(day => day.isToday && day.events.length) ?? null;
  }

  // The grid as one list. Written out rather than with Array.flat(), which this
  // project's TypeScript target does not carry.
  private allDays(): CalendarDay[] {
    const days: CalendarDay[] = [];
    for (const week of this.weeks) days.push(...week);
    return days;
  }

  // --- Presentation ---------------------------------------------------------

  get monthTotals(): { pickups: number; returns: number; requests: number } {
    const days = this.allDays().filter(day => day.inMonth);

    return {
      pickups: days.reduce((sum, day) => sum + day.pickups, 0),
      returns: days.reduce((sum, day) => sum + day.returns, 0),
      requests: days.reduce((sum, day) => sum + day.requests, 0)
    };
  }

  get hasAnyEvent(): boolean {
    return this.weeks.some(week => week.some(day => day.events.length > 0));
  }

  icon(kind?: BookingCalendarEventKind): string {
    switch (kind) {
      case BookingCalendarEventKind.Pickup: return 'logout';
      case BookingCalendarEventKind.Return: return 'login';
      default: return 'event_available';
    }
  }

  labelKey(kind?: BookingCalendarEventKind): string {
    switch (kind) {
      case BookingCalendarEventKind.Pickup: return 'calendar.pickup';
      case BookingCalendarEventKind.Return: return 'calendar.return';
      default: return 'calendar.request';
    }
  }

  /** What the entry's own record calls its state — the lists' wording, reused. */
  stateLabelKey(event: BookingCalendarEventDto): string {
    if (event.kind === BookingCalendarEventKind.ReservationStart) {
      return RESERVATION_STATUS_KEYS[event.reservationStatus as ReservationStatus] ?? '';
    }

    return RENTING_STATE_KEYS[event.rentingState as RentingState] ?? '';
  }

  // Chip tone, matching the bookings lists: running now reads as good, still to
  // come as informational, a hold awaiting the agency as a warning — and a hire
  // that should already be back as a problem, which is the one the desk is
  // looking for.
  stateClass(event: BookingCalendarEventDto): string {
    if (event.isLate) return 'danger';

    if (event.kind === BookingCalendarEventKind.ReservationStart) {
      return event.reservationStatus === ReservationStatus.PendingConfirmation ? 'warn' : 'info';
    }

    switch (event.rentingState) {
      case RentingState.InProgress: return 'ok';
      case RentingState.NotYet: return 'info';
      default: return 'neutral';
    }
  }

  /** Where an entry leads, or null when this user may not open it. */
  linkFor(event: BookingCalendarEventDto): string[] | null {
    if (event.kind === BookingCalendarEventKind.ReservationStart) {
      return this.canOpenReservation && event.reservationId
        ? ['/reservation', String(event.reservationId)]
        : null;
    }

    return this.canOpenRenting && event.rentingId
      ? ['/renting', String(event.rentingId)]
      : null;
  }

  /** The car, however much of it the entry knows. */
  carLabel(event: BookingCalendarEventDto): string {
    return event.carMatricule || event.carModelName || '';
  }
}

// The enum → transloco key maps the lists use, kept here so the calendar says
// "Pending" and "In progress" in exactly the words the bookings screens do.
const RENTING_STATE_KEYS: Record<RentingState, string> = {
  [RentingState.NotYet]: 'enums.rentingState.notYet',
  [RentingState.InProgress]: 'enums.rentingState.inProgress',
  [RentingState.Done]: 'enums.rentingState.done',
  [RentingState.Cancelled]: 'enums.rentingState.cancelled'
};

const RESERVATION_STATUS_KEYS: Record<ReservationStatus, string> = {
  [ReservationStatus.PendingConfirmation]: 'enums.reservationStatus.pendingConfirmation',
  [ReservationStatus.Confirmed]: 'enums.reservationStatus.confirmed',
  [ReservationStatus.Paid]: 'enums.reservationStatus.paid',
  [ReservationStatus.Converted]: 'enums.reservationStatus.converted',
  [ReservationStatus.Rejected]: 'enums.reservationStatus.rejected',
  [ReservationStatus.Cancelled]: 'enums.reservationStatus.cancelled',
  [ReservationStatus.Expired]: 'enums.reservationStatus.expired'
};

// --- Local calendar arithmetic ----------------------------------------------
// All local, all midnight: the grid is a wall calendar, and its cells are the
// same calendar days the rest of the app's date fields hold.

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function addMonths(date: Date, months: number): Date {
  return new Date(date.getFullYear(), date.getMonth() + months, 1);
}

function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

/** 0 for Monday … 6 for Sunday — how far into its week a date sits. */
function mondayIndex(date: Date): number {
  return (date.getDay() + 6) % DAYS_PER_WEEK;
}

/**
 * The same calendar day as UTC midnight, which is what the API reads a date as
 * (see form-utils' fromDateInput — the generated client sends toISOString()).
 */
function asUtcMidnight(date: Date): Date {
  return new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
}
