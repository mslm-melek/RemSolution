import { Component, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { Observable, Subscription, of, timer } from 'rxjs';
import { map } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../shared/auth.service';
import { BranchScopeService } from '../shared/branch-scope.service';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import { ImpersonationService } from '../shared/impersonation.service';
import { extractValidationErrors, toUtcDateInput } from '../shared/form-utils';
import {
  HomeWidgetMeta, MAX_HOME_WIDGETS, availableHomeWidgets, countTiles, resolveHomeWidgets
} from '../shared/home-widgets';
import { HomeAgendaComponent } from './home-agenda.component';
import {
  BrandsClient, CarsClient, ChatClient, ClientsClient, CreditsClient,
  CurrentUserDto, DashboardClient, DocumentTemplatesClient, ExpenseDueBasis,
  ExpenseTypesClient, ExpensesClient, ExtraServiceTypesClient, MarketplaceCarDto,
  MarketplaceClient, ModelCarsClient, RentingState, RentingsClient, ReservationDto,
  TodayDto, TodayExpenseCarDto, TodayExpenseGroupDto, TodayRequestDto,
  UpdateMyHomeWidgetsCommand, UsersClient
} from '../web-api-client';

// How long each car stays on screen in the home-page slideshow.
const SLIDE_INTERVAL_MS = 6_000;
// Slides in the shop window. More than this and nobody reaches the end.
const SHOWCASE_SIZE = 8;

// How often the "updated N minutes ago" line is redrawn. The figures on this
// screen go stale the moment a colleague hands a car over, and a page that
// quietly lies for an hour is worse than one that admits its age.
const STALE_TICK_MS = 60_000;
// Past this the line turns amber: long enough that somebody else has probably
// acted, short enough that a busy morning is not permanently orange.
const STALE_WARN_MINUTES = 5;

const MS_PER_HOUR = 3_600_000;
const MS_PER_DAY = 24 * MS_PER_HOUR;

/**
 * The landing screen.
 *
 * Four different screens live here, because "home" means four different things:
 * a shop window for a visitor, a browse-oriented start for a customer, the
 * console for the platform administrator, and — the one this file is mostly
 * about — TODAY for an agency's desk.
 *
 * The desk's version answers three questions in order: what does today ask for,
 * what is waiting on somebody, and what is the fleet doing. All of it comes from
 * ONE call (see GetTodayQuery), so nothing on the screen disagrees with anything
 * else on it, and every section the caller's modules do not cover is absent from
 * the answer rather than rendered empty.
 */
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly transloco = inject(TranslocoService);
  private readonly branchScope = inject(BranchScopeService);
  private readonly actions = inject(BookingActionsService);

  // The agenda reloads itself after an action; so does the rest of the screen.
  @ViewChild(HomeAgendaComponent) agenda?: HomeAgendaComponent;

  isAuthenticated: boolean | null = null;
  isPlatformAdmin = false;
  isCustomer = false;
  displayName: string | null | undefined;

  // Shop-window slideshow, shown to visitors and customers (staff get their day
  // instead). Cars come from the public marketplace, so an anonymous visitor can
  // see them before signing in.
  showcase: MarketplaceCarDto[] = [];
  slide = 0;
  private autoplay?: Subscription;
  private showcaseRequested = false;

  // --- Today ----------------------------------------------------------------

  today?: TodayDto;
  todayLoading = false;
  /** Whole minutes since the figures on screen were read. */
  staleMinutes = 0;

  branchId: number | null = null;

  /** Which recurring costs are showing their cars, by expense-type id. */
  private openExpenseGroups = new Set<number>();
  /** Whether the pending-requests card is showing its rows. */
  requestsOpen = false;

  /** An action's refusal, shown once above the cards. Clears itself. */
  message = '';

  private ticker?: Subscription;
  private branchSubscription?: Subscription;
  private loadedAt = Date.now();

  // --- Agency home: the shortcuts the user pinned ----------------------------

  availableWidgets: HomeWidgetMeta[] = [];
  pinnedWidgets: HomeWidgetMeta[] = [];
  // Counts, by widget key: undefined while loading, so a tile shows "—" rather
  // than a zero it has not confirmed.
  counts: Record<string, number | undefined> = {};

  // Open while the user is choosing their shortcuts. `draft` is the selection
  // being edited — the tiles on screen only change once it is saved.
  customizing = false;
  draft: string[] = [];
  saving = false;
  saveError = '';

  readonly maxWidgets = MAX_HOME_WIDGETS;
  readonly ExpenseDueBasis = ExpenseDueBasis;

  constructor(
    private auth: AuthService,
    private impersonation: ImpersonationService,
    private usersClient: UsersClient,
    private dashboardClient: DashboardClient,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private rentingsClient: RentingsClient,
    private expensesClient: ExpensesClient,
    private expenseTypesClient: ExpenseTypesClient,
    private extraServiceTypesClient: ExtraServiceTypesClient,
    private creditsClient: CreditsClient,
    private chatClient: ChatClient,
    private documentTemplatesClient: DocumentTemplatesClient,
    private modelCarsClient: ModelCarsClient,
    private brandsClient: BrandsClient,
    private marketplaceClient: MarketplaceClient
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

      // Customers get a browse-oriented home, not the desk's one (and none of the
      // staff calls, which they aren't authorized for).
      if (!this.isAuthenticated || this.isCustomer) {
        this.loadShowcase();
        return;
      }

      // The platform admin's landing screen IS the console dashboard, rendered
      // straight into the home route — so nothing else to set up here.
      if (!this.isPlatformAdmin) {
        this.setUpAgencyHome(user);
      }
    });
  }

  ngOnDestroy() {
    this.autoplay?.unsubscribe();
    this.ticker?.unsubscribe();
    this.branchSubscription?.unsubscribe();
    // The picker in the app bar is drawn from what this screen published, so it
    // goes away with the screen (see BranchScopeService).
    this.branchScope.clear();
  }

  // --- The desk's day -------------------------------------------------------

  private setUpAgencyHome(user: CurrentUserDto) {
    // A platform admin working inside an agency workspace counts as that agency's
    // administrator, exactly as the navigation's Configuration menu does.
    const isAgencyAdmin = user.role === 'AgencyAdministrator' || !!this.impersonation.current;

    this.availableWidgets = availableHomeWidgets(user, isAgencyAdmin);

    this.auth.homeWidgets$.subscribe(stored => {
      this.pinnedWidgets = resolveHomeWidgets(stored, this.availableWidgets);
      this.loadCounts();
    });

    // currentUser$ can emit more than once; the day is subscribed to once.
    if (this.branchSubscription) return;

    // Loads on subscribe, and again whenever the app bar's picker moves.
    this.branchSubscription = this.branchScope.branchId$.subscribe(branchId => {
      this.branchId = branchId;
      this.loadToday();
    });

    this.ticker = timer(STALE_TICK_MS, STALE_TICK_MS).subscribe(() => {
      this.staleMinutes = Math.floor((Date.now() - this.loadedAt) / STALE_TICK_MS);
    });
  }

  /** Everything on the screen again — after an action, or on the user's word. */
  refresh() {
    this.loadToday();
    this.agenda?.reload();
  }

  private loadToday() {
    this.todayLoading = true;

    // The browser's calendar day, sent as UTC midnight: the API's dates are
    // wall-clock values stamped UTC, so this is what makes a car booked out on
    // the 5th belong to the 5th whatever the offset (see form-utils).
    const now = new Date();
    const day = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));

    this.dashboardClient.getToday(day, this.branchId).subscribe({
      next: result => {
        this.todayLoading = false;
        this.today = result;
        this.loadedAt = Date.now();
        this.staleMinutes = 0;
        // The app bar's picker is drawn from this — see BranchScopeService.
        this.branchScope.publish(result.branches);
        // The pending-requests figure is one of these, so a pinned tile counting
        // the same thing takes it from here rather than asking again.
        this.counts['Reservations'] = result.requests?.count;
      },
      // A landing page is not the place for a banner about the whole screen: the
      // sections simply do not appear, and the refresh control is right there.
      error: err => {
        this.todayLoading = false;
        console.error(err);
      }
    });
  }

  get isStale(): boolean {
    return this.staleMinutes >= STALE_WARN_MINUTES;
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

  // --- "Needs your answer" --------------------------------------------------

  /** Whole hours the oldest unanswered request has been waiting. */
  get requestsWaitingHours(): number | null {
    const asked = this.today?.requests?.oldestAskedAt;
    if (!asked) return null;

    return Math.max(Math.floor((Date.now() - asked.getTime()) / MS_PER_HOUR), 0);
  }

  /** Nothing in either queue — said once rather than as two empty cards. */
  get nothingToAnswer(): boolean {
    const today = this.today;
    if (!today) return false;

    return !today.requests?.count && !today.payables?.count;
  }

  toggleRequests() {
    this.requestsOpen = !this.requestsOpen;
  }

  confirmRequest(request: TodayRequestDto) {
    this.act(this.actions.confirmReservation(new ReservationDto({ id: request.reservationId })));
  }

  rejectRequest(request: TodayRequestDto) {
    this.act(this.actions.rejectReservation(new ReservationDto({ id: request.reservationId })));
  }

  private act(action: Observable<BookingActionOutcome>) {
    action.subscribe(outcome => {
      if (outcome.error) this.show(outcome.error);
      if (outcome.changed) this.refresh();
    });
  }

  private show(text: string) {
    this.message = text;
    setTimeout(() => this.message = '', 6000);
  }

  // --- Expenses due ---------------------------------------------------------

  /** Cars owing something, across every recurring cost. */
  get expensesDueCount(): number {
    return (this.today?.expensesDue ?? []).reduce((sum, g) => sum + (g.cars?.length ?? 0), 0);
  }

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

  /** "every 10 000 km", "every 12 months", or both — the group's subtitle. */
  ruleKey(group: TodayExpenseGroupDto): string {
    if (group.afterMonth && group.afterKilometer) return 'home.due.ruleBoth';
    return group.afterKilometer ? 'home.due.ruleDistance' : 'home.due.ruleMonths';
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

  // --- Pinned shortcuts ------------------------------------------------------

  /** The count tiles, in the row. A panel widget is not one of them. */
  get pinnedTiles(): HomeWidgetMeta[] {
    return this.pinnedWidgets.filter(w => !w.panel);
  }

  // Only the tiles on screen are counted, and each is counted once: revisiting
  // the page after pinning something new fetches the new tile alone.
  private loadCounts() {
    for (const widget of this.pinnedTiles) {
      if (widget.key in this.counts) continue;

      // Reserve the slot before the call so a second pass cannot re-request it.
      this.counts[widget.key] = undefined;

      // Left to the day's own payload, which is on the same screen and asks the
      // same question (see loadToday).
      if (widget.key === 'Reservations') continue;

      this.countOf(widget.key).subscribe({
        next: value => this.counts[widget.key] = value,
        // A tile that cannot be counted keeps its "—" and stays a working link;
        // the landing page is not the place for an error banner about one figure.
        error: err => console.error(err)
      });
    }
  }

  // Each tile counts what its label says. Where a list has an obvious "needs
  // doing" subset (running rentings, unpaid expenses), the tile counts that
  // rather than the whole table — a total nobody acts on is decoration. Page
  // size 1: only totalCount is wanted.
  //
  // Whatever is filtered here MUST match the tile's `queryParams` in
  // shared/home-widgets, or clicking the figure opens a list that disagrees
  // with it.
  private countOf(key: string): Observable<number> {
    switch (key) {
      case 'Cars':
        return this.carsClient
          .getCars(1, 1, null, null, null, null, null, null, null, null, null, null, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Clients':
        return this.clientsClient.getClients(1, 1, null, null, null, null, null, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Rentings':
        return this.rentingsClient
          .getRentings(
            1, 1, null, null, null, RentingState.InProgress,
            null, null, undefined, false, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      // 'Reservations' is not here: the day's payload already counted the holds
      // awaiting the agency, and the tile takes its figure from there.
      case 'Expenses':
        return this.expensesClient
          .getExpenses(1, 1, null, null, null, null, true, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Credits':
        return this.creditsClient.getClientCredits(1, 1, true, null, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Chat':
        return this.chatClient.getThreads(1, 1, true)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Brands':
        return this.brandsClient.getBrands().pipe(map(r => (r || []).length));
      case 'CarModels':
        return this.modelCarsClient.getModelCars(1, 1, null, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'ExpenseTypes':
        return this.expenseTypesClient.getExpenseTypes(true).pipe(map(r => (r || []).length));
      case 'ExtraServiceTypes':
        return this.extraServiceTypesClient.getExtraServiceTypes(true).pipe(map(r => (r || []).length));
      case 'DocumentTemplates':
        return this.documentTemplatesClient.getDocumentTemplates(null, null, false)
          .pipe(map(r => (r || []).length));
      default:
        return of(0);
    }
  }

  // --- Customizing ----------------------------------------------------------

  startCustomizing() {
    this.draft = this.pinnedWidgets.map(w => w.key);
    this.saveError = '';
    this.customizing = true;
  }

  cancelCustomizing() {
    this.customizing = false;
    this.saveError = '';
  }

  isPinned(key: string): boolean {
    return this.draft.includes(key);
  }

  // The pinned list is what the row shows in order; everything else is offered
  // below it. Newly checked tiles join the end, where the user can see them.
  toggleWidget(key: string) {
    if (this.isPinned(key)) {
      this.draft = this.draft.filter(k => k !== key);
      return;
    }

    if (this.isFull) return;

    this.draft = [...this.draft, key];
  }

  get draftWidgets(): HomeWidgetMeta[] {
    return this.draft
      .map(key => this.availableWidgets.find(w => w.key === key))
      .filter((w): w is HomeWidgetMeta => w !== undefined);
  }

  get unpinnedWidgets(): HomeWidgetMeta[] {
    return this.availableWidgets.filter(w => !this.isPinned(w.key));
  }

  get isFull(): boolean {
    return countTiles(this.draft, this.availableWidgets) >= MAX_HOME_WIDGETS;
  }

  // Up/down rather than drag: it works from the keyboard, and it mirrors itself
  // in Arabic without any right-to-left handling.
  moveUp(index: number) {
    if (index <= 0) return;
    const next = [...this.draft];
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.draft = next;
  }

  moveDown(index: number) {
    if (index >= this.draft.length - 1) return;
    const next = [...this.draft];
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    this.draft = next;
  }

  saveWidgets() {
    this.saving = true;
    this.saveError = '';

    const widgets = [...this.draft];

    this.usersClient.updateMyHomeWidgets(new UpdateMyHomeWidgetsCommand({ widgets })).subscribe({
      next: () => {
        this.saving = false;
        this.customizing = false;
        this.pinnedWidgets = resolveHomeWidgets(widgets, this.availableWidgets);
        // The current-user probe is fetched once per page load, so the service
        // has to be told — otherwise coming back to the home screen would show
        // the selection from before this save.
        this.auth.markHomeWidgets(widgets);
        this.loadCounts();
      },
      error: err => {
        this.saving = false;
        this.saveError =
          extractValidationErrors(err) ?? this.transloco.translate('home.widgetsSaveFailed');
      }
    });
  }

  // --- Slideshow ------------------------------------------------------------

  // Advancing on a click also stops the timer: a card must not slide away from
  // under someone who has taken control of the slideshow.
  prevSlide() {
    this.autoplay?.unsubscribe();
    this.slide = (this.slide - 1 + this.showcase.length) % this.showcase.length;
  }

  nextSlide() {
    this.autoplay?.unsubscribe();
    this.slide = (this.slide + 1) % this.showcase.length;
  }

  goToSlide(index: number) {
    this.autoplay?.unsubscribe();
    this.slide = index;
  }

  private loadShowcase() {
    // currentUser$ can emit more than once; the slideshow is loaded once.
    if (this.showcaseRequested) {
      return;
    }
    this.showcaseRequested = true;

    this.marketplaceClient.getShowcaseCars(SHOWCASE_SIZE).subscribe({
      next: cars => {
        this.showcase = cars || [];
        if (this.showcase.length > 1) {
          this.autoplay = timer(SLIDE_INTERVAL_MS, SLIDE_INTERVAL_MS)
            .subscribe(() => this.slide = (this.slide + 1) % this.showcase.length);
        }
      },
      // An empty shop window is not worth an error banner on the landing page.
      error: err => console.error(err)
    });
  }
}
