import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { AuthService } from '../shared/auth.service';
import { BranchScopeService } from '../shared/branch-scope.service';
import { BookingActionsService } from '../shared/booking-actions.service';
import { ImpersonationService } from '../shared/impersonation.service';
import { toUtcDateInput, wallClockNow } from '../shared/form-utils';
import {
  BookingCalendarEventDto, BookingCalendarEventKind, CurrentUserDto, DashboardClient,
  ExpenseDueBasis, RentingDto, ReservationStatus, TodayDto, TodayExpenseCarDto,
  TodayExpenseGroupDto, TodayLateRentingDto
} from '../web-api-client';

const MS_PER_DAY = 24 * 3_600_000;

/**
 * The landing screen.
 *
 * Four different screens live here, because "home" means four different things:
 * a shop window for a visitor, the marketplace search itself for a customer,
 * the console for the platform administrator, and — the one this file is mostly
 * about — TODAY for an agency's desk. The last three are all screens that used
 * to live behind a route of their own; each is its own home because a nav with
 * both entries only ever asked the user which of two identical pages they meant.
 *
 * The desk's version answers three questions in order: how many cars move today,
 * what is waiting on the desk, and who comes in when. The figures come from ONE
 * call (see GetTodayQuery), so nothing on the screen disagrees with anything else
 * on it, and every section the caller's modules do not cover is absent from the
 * answer rather than rendered empty. The day's list is the booking calendar read
 * for today alone.
 */
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly branchScope = inject(BranchScopeService);
  private readonly actions = inject(BookingActionsService);

  isAuthenticated: boolean | null = null;
  isPlatformAdmin = false;
  isCustomer = false;
  displayName: string | null | undefined;

  // --- The customer's home --------------------------------------------------

  // The search itself is MarketplaceSearchComponent, rendered by the template,
  // and it holds its own state — so all this screen owns is the copy below.

  // Three lines about how a booking works. A customer's first visit is the one
  // where they decide whether to hand us a passport scan.
  readonly steps = [
    { icon: 'search', key: 'one' },
    { icon: 'event_available', key: 'two' },
    { icon: 'directions_car', key: 'three' }
  ];

  // --- Today ----------------------------------------------------------------

  today?: TodayDto;
  todayLoading = false;
  /** Today's pickups and returns, in time order. Undefined until read. */
  agenda?: BookingCalendarEventDto[];

  branchId: number | null = null;

  // What this user may start or do from here; the server decides what they see.
  canCreateRenting = false;
  canActOnRentings = false;

  /** Which recurring costs are showing their cars, by expense-type id. */
  private openExpenseGroups = new Set<number>();

  /** An action's refusal, shown once above the cards. Clears itself. */
  message = '';

  private branchSubscription?: Subscription;

  readonly ReservationStatus = ReservationStatus;

  constructor(
    private auth: AuthService,
    private impersonation: ImpersonationService,
    private dashboardClient: DashboardClient
  ) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      // A platform admin inside an agency workspace is looking at that agency, so
      // the tenant-scoped screen is the right one — the console dashboard belongs
      // outside the workspace.
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user) && !this.impersonation.current;
      this.isCustomer = AuthService.isCustomer(user);
      this.displayName = user.fullName || user.userName;

      // Visitors get PublicHomeComponent and customers the search, both of which
      // load their own cars — and neither may call the staff endpoints.
      if (!this.isAuthenticated || this.isCustomer) return;

      // The platform admin's landing screen IS the console dashboard, rendered
      // straight into the home route — so nothing else to set up here.
      if (!this.isPlatformAdmin) {
        this.setUpAgencyHome(user);
      }
    });
  }

  ngOnDestroy() {
    this.branchSubscription?.unsubscribe();
    // The picker in the app bar is drawn from what this screen published, so it
    // goes away with the screen (see BranchScopeService).
    this.branchScope.clear();
  }

  // --- The desk's day -------------------------------------------------------

  private setUpAgencyHome(user: CurrentUserDto) {
    this.canCreateRenting = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');
    this.canActOnRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');

    // currentUser$ can emit more than once; the day is subscribed to once.
    if (this.branchSubscription) return;

    // Loads on subscribe, and again whenever the app bar's picker moves.
    this.branchSubscription = this.branchScope.branchId$.subscribe(branchId => {
      this.branchId = branchId;
      this.refresh();
    });
  }

  /** Everything on the screen again — on load, on a branch change, after an action. */
  refresh() {
    this.loadToday();
    this.loadAgenda();
  }

  /**
   * The browser's calendar day, sent as UTC midnight: the API's dates are
   * wall-clock values stamped UTC, so this is what makes a car booked out on
   * the 5th belong to the 5th whatever the offset (see form-utils).
   */
  private get day(): Date {
    const now = new Date();
    return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
  }

  private loadToday() {
    this.todayLoading = true;

    this.dashboardClient.getToday(this.day, this.branchId).subscribe({
      next: result => {
        this.todayLoading = false;
        this.today = result;
        // The app bar's picker is drawn from this — see BranchScopeService.
        this.branchScope.publish(result.branches);
      },
      // A landing page is not the place for a banner about the whole screen: the
      // sections simply do not appear.
      error: err => {
        this.todayLoading = false;
        console.error(err);
      }
    });
  }

  private loadAgenda() {
    const day = this.day;

    this.dashboardClient.getBookingCalendar(day, new Date(day.getTime() + MS_PER_DAY), this.branchId)
      .subscribe({
        // A car going to the garage is not somebody walking in, so the list is
        // pickups, returns and holds starting.
        next: result => this.agenda = (result.events ?? [])
          .filter(e => e.kind !== BookingCalendarEventKind.UnavailabilityStart),
        error: err => {
          this.agenda = [];
          console.error(err);
        }
      });
  }

  /** Transloco key under `home.greeting.*`, from the local hour. */
  get greetingKey(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'home.greeting.morning';
    if (hour < 18) return 'home.greeting.afternoon';
    return 'home.greeting.evening';
  }

  /** The branch named under the greeting, when one is chosen. */
  get branchName(): string | null {
    return this.branchScope.nameOf(this.branchId);
  }

  // --- Needs you ------------------------------------------------------------

  /** Nothing late, nothing to answer, nothing owed, nothing due. Said once. */
  get nothingNeeded(): boolean {
    const today = this.today;
    if (!today) return false;

    return !today.summary?.worstLate
      && !today.requests?.count
      && !today.payables?.count
      && !today.expensesDue?.length;
  }

  /** Requests counted but not listed — the payload carries the oldest few. */
  get moreRequests(): number {
    const requests = this.today?.requests;
    return (requests?.count ?? 0) - (requests?.items?.length ?? 0);
  }

  /** Takes the car back. The dialog re-reads the hire itself, so the id is enough. */
  takeBack(late: TodayLateRentingDto) {
    this.actions.returnRenting(new RentingDto({ id: late.rentingId, clientName: late.clientName }))
      .subscribe(outcome => {
        if (outcome.error) this.show(outcome.error);
        if (outcome.changed) this.refresh();
      });
  }

  private show(text: string) {
    this.message = text;
    setTimeout(() => this.message = '', 6000);
  }

  // --- Expenses due ---------------------------------------------------------

  isExpenseGroupOpen(group: TodayExpenseGroupDto): boolean {
    return this.openExpenseGroups.has(group.expenseTypeId!);
  }

  toggleExpenseGroup(group: TodayExpenseGroupDto) {
    const id = group.expenseTypeId!;

    if (this.openExpenseGroups.has(id)) {
      this.openExpenseGroups.delete(id);
    } else {
      this.openExpenseGroups.add(id);
    }
  }

  /** The models owing this cost, each named once. */
  carNames(group: TodayExpenseGroupDto): string {
    return [...new Set((group.cars ?? []).map(c => c.modelName || c.matricule))].join(', ');
  }

  /**
   * Transloco key for what one car owes. Four sentences, one per (clock,
   * standing) pair — the same four the notification messages use, because a
   * screen and an inbox that word the same fact differently is how people stop
   * trusting both.
   */
  dueKey(car: TodayExpenseCarDto): string {
    const distance = car.basis === ExpenseDueBasis.Distance;

    if (car.isOverdue) return distance ? 'home.due.overKm' : 'home.due.overdueSince';
    return distance ? 'home.due.inKm' : 'home.due.dueIn';
  }

  /** Booking it: the expense form, already pointed at the car and the cost. */
  recordParams(group: TodayExpenseGroupDto, car: TodayExpenseCarDto) {
    return { car: car.carId, type: group.expenseTypeId };
  }

  // --- Today's list ---------------------------------------------------------

  /** A car leaving: a hire's pickup, or a hold that starts. */
  isOut(event: BookingCalendarEventDto): boolean {
    return event.kind !== BookingCalendarEventKind.Return;
  }

  /**
   * Its hour has gone by. Compared against the wall clock rebuilt as a UTC
   * instant, not Date.now(): the API's times are wall-clock values stamped UTC.
   */
  isPast(event: BookingCalendarEventDto): boolean {
    return !event.isLate && !!event.on && event.on.getTime() < wallClockNow();
  }

  agendaLink(event: BookingCalendarEventDto): unknown[] {
    return event.rentingId ? ['/renting', event.rentingId] : ['/reservation', event.reservationId];
  }

  // --- Where the day's figures lead -----------------------------------------
  // Every link carries the filter its figure was counted with, so the list opens
  // showing exactly the rows the card counted (see shared/list-filters).

  private get dayWindow(): { from: string; to: string } {
    const day = this.today?.day ?? new Date();
    return { from: toUtcDateInput(day), to: toUtcDateInput(new Date(day.getTime() + MS_PER_DAY)) };
  }

  get bookingsTodayParams() {
    return { dateBasis: 'Starts', excludeCancelled: 'true', ...this.dayWindow };
  }

  get returnsTodayParams() {
    return { state: 'InProgress', dateBasis: 'Ends', ...this.dayWindow };
  }

  /** Due back strictly before today — which is what "late" counts. */
  get lateParams() {
    return { state: 'InProgress', dateBasis: 'Ends', to: this.dayWindow.from };
  }

  readonly pendingParams = { status: 'PendingConfirmation' };
  readonly payableParams = { tab: 'expenses', unpaid: 'true' };
  readonly freeCarsParams = { status: 'Active', onRent: 'false' };
}
