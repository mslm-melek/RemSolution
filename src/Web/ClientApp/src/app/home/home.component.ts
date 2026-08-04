import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Observable, Subscription, of, timer } from 'rxjs';
import { map } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../shared/auth.service';
import { ImpersonationService } from '../shared/impersonation.service';
import { extractValidationErrors } from '../shared/form-utils';
import {
  HomeWidgetMeta, MAX_HOME_WIDGETS, availableHomeWidgets, countTiles, resolveHomeWidgets
} from '../shared/home-widgets';
import {
  BrandsClient, CarsClient, ChatClient, ClientsClient, CreditsClient,
  CurrentUserDto, DocumentTemplatesClient, ExpenseTypesClient, ExpensesClient,
  ExtraServiceTypesClient, MarketplaceCarDto, MarketplaceClient, ModelCarsClient,
  RentingState, RentingsClient, ReservationStatus, ReservationsClient,
  UpdateMyHomeWidgetsCommand, UsersClient
} from '../web-api-client';

// How long each car stays on screen in the home-page slideshow.
const SLIDE_INTERVAL_MS = 6_000;
// Slides in the shop window. More than this and nobody reaches the end.
const SHOWCASE_SIZE = 8;

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly transloco = inject(TranslocoService);

  isAuthenticated: boolean | null = null;
  isPlatformAdmin = false;
  isCustomer = false;
  displayName: string | null | undefined;

  // Shop-window slideshow, shown to visitors and customers (staff get their
  // pinned tiles instead). Cars come from the public marketplace, so an anonymous
  // visitor can see them before signing in.
  showcase: MarketplaceCarDto[] = [];
  slide = 0;
  private autoplay?: Subscription;
  private showcaseRequested = false;

  // --- Agency home: the widgets the user pinned -----------------------------

  // Everything this user could pin, and what they have pinned, in their order.
  availableWidgets: HomeWidgetMeta[] = [];
  pinnedWidgets: HomeWidgetMeta[] = [];
  // Counts, by widget key: undefined while loading, so a tile shows "—" rather
  // than a zero it has not confirmed.
  counts: Record<string, number | undefined> = {};

  // Open while the user is choosing their tiles. `draft` is the selection being
  // edited — the tiles on screen only change once it is saved.
  customizing = false;
  draft: string[] = [];
  saving = false;
  saveError = '';

  readonly maxWidgets = MAX_HOME_WIDGETS;

  constructor(
    private auth: AuthService,
    private impersonation: ImpersonationService,
    private usersClient: UsersClient,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private rentingsClient: RentingsClient,
    private reservationsClient: ReservationsClient,
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
      // the tenant-scoped tiles and quick actions are the right ones — the console
      // dashboard belongs outside the workspace.
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user) && !this.impersonation.current;
      this.isCustomer = AuthService.isCustomer(user);
      this.displayName = user.fullName || user.userName;

      // Customers get a browse-oriented home, not the staff one (and none of the
      // staff count calls, which they aren't authorized for).
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
  }

  // --- Pinned tiles ---------------------------------------------------------

  private setUpAgencyHome(user: CurrentUserDto) {
    // A platform admin working inside an agency workspace counts as that agency's
    // administrator, exactly as the navigation's Configuration menu does: the
    // reference-data screens accept either administrator role.
    const isAgencyAdmin = user.role === 'AgencyAdministrator' || !!this.impersonation.current;

    this.availableWidgets = availableHomeWidgets(user, isAgencyAdmin);

    this.auth.homeWidgets$.subscribe(stored => {
      this.pinnedWidgets = resolveHomeWidgets(stored, this.availableWidgets);
      this.loadCounts();
    });
  }

  /** The count tiles, in the row. A panel widget is not one of them. */
  get pinnedTiles(): HomeWidgetMeta[] {
    return this.pinnedWidgets.filter(w => !w.panel);
  }

  /** Whether a given panel widget — the calendar — is pinned. */
  hasPanel(key: string): boolean {
    return this.pinnedWidgets.some(w => w.panel && w.key === key);
  }

  // Only the tiles on screen are counted, and each is counted once: revisiting
  // the page after pinning something new fetches the new tile alone. Panels do
  // their own loading, so they are not here.
  private loadCounts() {
    for (const widget of this.pinnedTiles) {
      if (widget.key in this.counts) continue;

      // Reserve the slot before the call so a second pass cannot re-request it.
      this.counts[widget.key] = undefined;

      this.countOf(widget.key).subscribe({
        next: value => this.counts[widget.key] = value,
        // A tile that cannot be counted keeps its "—" and stays a working link;
        // the landing page is not the place for an error banner about one figure.
        error: err => console.error(err)
      });
    }
  }

  // Each tile counts what its label says. Where a list has an obvious "needs
  // doing" subset (unconfirmed requests, running rentings, unpaid expenses), the
  // tile counts that rather than the whole table — a total nobody acts on is
  // decoration. Page size 1: only totalCount is wanted.
  //
  // Whatever is filtered here MUST match the tile's `queryParams` in
  // shared/home-widgets, or clicking the figure opens a list that disagrees
  // with it.
  private countOf(key: string): Observable<number> {
    switch (key) {
      case 'Cars':
        return this.carsClient.getCars(1, 1, null, null, null, null, null, null, null, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Clients':
        return this.clientsClient.getClients(1, 1, null, null, null, null, null, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Rentings':
        return this.rentingsClient
          .getRentings(
            1, 1, null, null, RentingState.InProgress, null, null, undefined, false, null, false)
          .pipe(map(r => r.totalCount ?? 0));
      case 'Reservations':
        return this.reservationsClient
          .getReservations(1, 1, null, null, ReservationStatus.PendingConfirmation, null, false)
          .pipe(map(r => r.totalCount ?? 0));
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
    // Tiles first, panels after: the order buttons only reorder the row, so a
    // panel sitting between two tiles would make "move up" look broken. Every
    // path that adds to the draft keeps that arrangement.
    const pinned = this.pinnedWidgets.map(w => w.key);
    this.draft = [...pinned.filter(k => !this.isPanel(k)), ...pinned.filter(k => this.isPanel(k))];
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

  // The pinned list is what the panel shows in order; everything else is offered
  // below it. Newly checked tiles join the end, where the user can see them.
  toggleWidget(key: string) {
    if (this.isPinned(key)) {
      this.draft = this.draft.filter(k => k !== key);
      return;
    }

    // The cap is on the tile row, so a panel is always addable (see home-widgets).
    if (this.isPanel(key)) {
      this.draft = [...this.draft, key];
      return;
    }

    if (this.isFull) return;

    // A newly checked tile joins the end of the row — which is before the panels,
    // not after them (see startCustomizing).
    const firstPanel = this.draft.findIndex(k => this.isPanel(k));
    this.draft = firstPanel < 0
      ? [...this.draft, key]
      : [...this.draft.slice(0, firstPanel), key, ...this.draft.slice(firstPanel)];
  }

  isPanel(key: string): boolean {
    return this.availableWidgets.find(w => w.key === key)?.panel === true;
  }

  get draftWidgets(): HomeWidgetMeta[] {
    return this.draft
      .map(key => this.availableWidgets.find(w => w.key === key))
      .filter((w): w is HomeWidgetMeta => w !== undefined);
  }

  get unpinnedWidgets(): HomeWidgetMeta[] {
    return this.availableWidgets.filter(w => !this.isPinned(w.key));
  }

  // Tiles only, exactly as the server validates it: a full row still has room for
  // the calendar underneath it.
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

  /** The last tile of the row: whatever follows it in the draft is a panel. */
  isLastTile(index: number): boolean {
    return this.draft.slice(index + 1).every(k => this.isPanel(k));
  }

  moveDown(index: number) {
    // Never past a panel: the panels sit after the row and stay there.
    if (this.isLastTile(index)) return;

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
        this.saveError = extractValidationErrors(err) ?? this.transloco.translate('home.widgetsSaveFailed');
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
