import { Component, OnInit, inject } from '@angular/core';
import { Directionality } from '@angular/cdk/bidi';
import { PageEvent } from '@angular/material/paginator';
import { SortDirection } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import {
  CarsClient, ClientsClient, MoneyDto,
  RentingDto, RentingDateBasis, RentingState, RentingsClient,
  ReservationDto, ReservationStatus, ReservationsClient
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';
import { CancelDialogComponent } from '../shared/cancel-dialog.component';
import {
  FilterChip, applyListFilters, boolParam, dateParam, enumName, enumParam, idParam, rangeText
} from '../shared/list-filters';
import {
  extractProblemDetail, extractValidationErrors, isInvalidTransition, wallClockNow
} from '../shared/form-utils';
import {
  BookingDetailComponent, BookingDetailData, BookingDetailResult, BookingKind
} from './booking-detail.component';

/** Which half of the screen is showing. Lives in the URL as `?tab=`. */
export type BookingTab = 'rentings' | 'reservations';

/**
 * A row as the card draws it. A hire and a hold answer the same five questions —
 * who, which car, when, how much, where in its life — so they are read into one
 * shape and rendered once. The DTO rides along for the actions, which are the
 * one part that genuinely differs.
 */
export interface BookingRow {
  kind: BookingKind;
  id: number;
  clientName?: string;
  carModelName?: string;
  carMatricule?: string;
  startDate?: Date;
  endDate?: Date;
  amount?: MoneyDto;
  /**
   * The second money line: what is still owed on a hire, or what a hold has
   * been paid of its price ({@link noteOf}). Numbers rather than a built string,
   * so the template formats them through the same pipe as the amount above —
   * `toFixed` would print 2350.00 under a 2 350,00.
   */
  noteAmount?: number;
  noteOf?: number;
  noteCurrency?: string;
  statusLabelKey: string;
  /** A global `.chip` tone, which also colours the card's leading edge. */
  tone: 'ok' | 'info' | 'pending' | 'danger' | 'neutral';
  /** Waiting on the agency — the bar in the leading gutter, and only then. */
  needsAction: boolean;
  /** Waiting on the agency because a deadline has already passed, not because
   *  one is coming: the same bar in red rather than amber. */
  overdue: boolean;
  /** Why a terminal booking ended, for the row's tooltip. */
  hint?: string;
  renting?: RentingDto;
  reservation?: ReservationDto;
}

interface SortOption {
  key: string;
  labelKey: string;
}

/**
 * Bookings: the hires and the holds on one screen, as two tabs over one list of
 * cards, with the booking that is clicked opening beside it.
 *
 * They were two screens, and being two cost more than it bought. A hold becomes a
 * hire (see ConvertReservationCommand) — they are the same booking at two points
 * of its life — so the counter's question is "what is happening with this car and
 * this customer", not "is this filed as a reservation or as a renting". The
 * filters, the money, the periods and half the actions were already the same in
 * both places, and keeping two of everything meant two of every fix.
 *
 * Cards rather than a table: a booking is read as a whole (who, which car, when,
 * how much, what state) and a row of aligned columns makes the eye assemble that
 * from five places. The order the cards come in is still the server's, chosen
 * from the toolbar — cards have no headers to click.
 *
 * Everything the list is narrowed by lives in the URL (see shared/list-filters),
 * so the home tiles and the dashboard's counts link straight to the rows they
 * counted, and the tab is part of that: `?tab=reservations` is what the old
 * /reservation route redirects to.
 */
@Component({
  selector: 'app-booking',
  templateUrl: './booking.component.html',
  styleUrls: ['./booking.component.css']
})
export class BookingComponent implements OnInit {
  private readonly dialog = inject(MatDialog);
  // Confirm/prompt boxes and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  // The panel is pinned to the edge the page ends on, which is the other edge in
  // Arabic. A dialog is positioned in absolute terms (the CDK overlay knows
  // nothing about the page's direction), so the side is chosen here rather than
  // left to a logical property in the stylesheet.
  private readonly direction = inject(Directionality);

  tab: BookingTab = 'rentings';
  rows: BookingRow[] = [];
  errorMessage = '';

  canPay = false;
  canChangeState = false;
  /** Whether each tab is reachable at all; a user with one of them gets no tabs. */
  canReadRentings = false;
  canReadReservations = false;

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  // --- Filters --------------------------------------------------------------
  // `state`/`status` and the search box have controls of their own; the rest
  // arrive by link — from a dashboard count, or from a car's or a client's
  // history — and show as removable chips, so the list never quietly shows a
  // subset. The window and the car/client narrowing mean the same thing on both
  // tabs and survive a tab switch; the rest belong to one tab and do not.
  search = '';
  state: RentingState | null = null;
  status: ReservationStatus | null = null;
  dateBasis: RentingDateBasis | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;
  excludeCancelled = false;
  activeOnly = false;
  carId: number | null = null;
  clientId: number | null = null;
  chips: FilterChip[] = [];

  // Sorting is server-side, and the key is the same one the old tables' column
  // ids were (see SortingExtensions' allow-list per handler). Each tab keeps its
  // own, because "expires" means nothing to a hire and "state" nothing to a hold.
  private sort: Record<BookingTab, { by: string; direction: SortDirection }> = {
    rentings: { by: 'period', direction: 'desc' },
    reservations: { by: 'period', direction: 'desc' }
  };

  RentingState = RentingState;
  ReservationStatus = ReservationStatus;

  states = [
    { value: RentingState.NotYet, labelKey: 'enums.rentingState.notYet' },
    { value: RentingState.InProgress, labelKey: 'enums.rentingState.inProgress' },
    { value: RentingState.Done, labelKey: 'enums.rentingState.done' },
    { value: RentingState.Cancelled, labelKey: 'enums.rentingState.cancelled' }
  ];

  statuses = [
    { value: ReservationStatus.PendingConfirmation, labelKey: 'enums.reservationStatus.pendingConfirmation' },
    { value: ReservationStatus.Confirmed, labelKey: 'enums.reservationStatus.confirmed' },
    { value: ReservationStatus.Paid, labelKey: 'enums.reservationStatus.paid' },
    { value: ReservationStatus.Converted, labelKey: 'enums.reservationStatus.converted' },
    { value: ReservationStatus.Rejected, labelKey: 'enums.reservationStatus.rejected' },
    { value: ReservationStatus.Cancelled, labelKey: 'enums.reservationStatus.cancelled' },
    { value: ReservationStatus.Expired, labelKey: 'enums.reservationStatus.expired' }
  ];

  private readonly rentingSorts: SortOption[] = [
    { key: 'period', labelKey: 'common.period' },
    { key: 'car', labelKey: 'renting.car' },
    { key: 'client', labelKey: 'renting.client' },
    { key: 'state', labelKey: 'renting.state' },
    { key: 'price', labelKey: 'common.price' }
  ];

  private readonly reservationSorts: SortOption[] = [
    { key: 'period', labelKey: 'common.period' },
    { key: 'car', labelKey: 'reservation.car' },
    { key: 'client', labelKey: 'reservation.client' },
    { key: 'paid', labelKey: 'reservation.paidPrice' },
    { key: 'status', labelKey: 'common.status' },
    { key: 'expires', labelKey: 'reservation.expires' }
  ];

  constructor(
    private rentings: RentingsClient,
    private reservations: ReservationsClient,
    private cars: CarsClient,
    private clients: ClientsClient,
    private auth: AuthService,
    private actions: BookingActionsService,
    private route: ActivatedRoute,
    private router: Router) { }

  // Which of the two subscriptions below answers first is not fixed —
  // currentUser$ is an HTTP call behind a shareReplay(1), so it emits
  // synchronously to a late subscriber (the shell has already asked) and
  // asynchronously to the first one. Neither order may leave the screen empty,
  // so the rows are never gated on the permissions having arrived: the tab is
  // clamped wherever it is known to be wrong, and whoever clamps it reloads.
  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
      this.canChangeState = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canReadRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canReadReservations = AuthService.canAccessModule(user, 'Reservations', 'Reservation.Read');

      if (this.clampTab()) this.load();
    });

    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);
      this.clampTab();
      this.pageNumber = 1;
      this.load();
    });
  }

  /**
   * A user who can reach only one of the two halves never sees the other's tab,
   * so the URL must not be able to land them on it either.
   *
   * @returns whether the tab had to move, which means the rows on screen (if
   *   any) are the other half's and have to be replaced.
   */
  private clampTab(): boolean {
    if (this.isRentings && !this.canReadRentings && this.canReadReservations) {
      this.tab = 'reservations';
      return true;
    }

    if (!this.isRentings && !this.canReadReservations && this.canReadRentings) {
      this.tab = 'rentings';
      return true;
    }

    return false;
  }

  get showTabs(): boolean {
    return this.canReadRentings && this.canReadReservations;
  }

  get isRentings(): boolean {
    return this.tab === 'rentings';
  }

  get sortBy(): string {
    return this.sort[this.tab].by;
  }

  get sortDirection(): SortDirection {
    return this.sort[this.tab].direction;
  }

  get sortOptions(): SortOption[] {
    return this.isRentings ? this.rentingSorts : this.reservationSorts;
  }

  get activeSortLabelKey(): string {
    return this.sortOptions.find(option => option.key === this.sortBy)?.labelKey ?? 'common.period';
  }

  /** The button that adds one: whichever kind of booking is being looked at. */
  get newLink(): string {
    return this.isRentings ? '/renting/new' : '/reservation/new';
  }

  // --- Reading the URL ------------------------------------------------------

  private readFilters(params: ParamMap) {
    this.tab = params.get('tab') === 'reservations' ? 'reservations' : 'rentings';

    this.search = params.get('search') ?? '';
    this.state = enumParam(params, 'state', RentingState) as RentingState | null;
    this.status = enumParam(params, 'status', ReservationStatus) as ReservationStatus | null;
    this.dateBasis = enumParam(params, 'dateBasis', RentingDateBasis) as RentingDateBasis | null;
    this.fromDate = dateParam(params, 'from');
    this.toDate = dateParam(params, 'to');
    this.excludeCancelled = boolParam(params, 'excludeCancelled') === true;
    this.activeOnly = boolParam(params, 'active') === true;
    this.carId = idParam(params, 'car');
    this.clientId = idParam(params, 'client');

    this.chips = [];

    // Arrived from a car's or a client's history count. The chip names the thing,
    // not its id, so it has to be looked up — until it answers, the id stands in.
    if (this.carId !== null) {
      const chip: FilterChip = {
        params: ['car'],
        labelKey: 'filters.rentingCar',
        labelArgs: { car: `#${this.carId}` }
      };
      this.chips.push(chip);
      this.cars.getCarById(this.carId).subscribe({
        next: car => chip.labelArgs = { car: car.matricule ?? chip.labelArgs!['car'] },
        // Without Car.Read the id is all this user can be told, which is enough
        // to see and clear the filter.
        error: () => { }
      });
    }

    if (this.clientId !== null) {
      const chip: FilterChip = {
        params: ['client'],
        labelKey: 'filters.rentingClient',
        labelArgs: { client: `#${this.clientId}` }
      };
      this.chips.push(chip);
      this.clients.getClientById(this.clientId).subscribe({
        next: c => chip.labelArgs = {
          client: [c.firstName, c.lastName].filter(Boolean).join(' ') || chip.labelArgs!['client']
        },
        error: () => { }
      });
    }

    if (this.fromDate || this.toDate) {
      const range = rangeText(params.get('from'), params.get('to'));
      // A hold has one date a day is planned by, its start; a hire can be windowed
      // on either end or on running through the window at all.
      const labelKey = !this.isRentings ? 'filters.reservationStarts'
        : this.dateBasis === RentingDateBasis.Starts ? 'filters.rentingStarts'
          : this.dateBasis === RentingDateBasis.Ends ? 'filters.rentingEnds'
            : 'filters.rentingRuns';
      this.chips.push({ params: ['from', 'to', 'dateBasis'], labelKey, labelArgs: { range } });
    }

    if (this.excludeCancelled && this.isRentings) {
      this.chips.push({ params: ['excludeCancelled'], labelKey: 'filters.excludingCancelled' });
    }

    if (this.activeOnly && !this.isRentings) {
      this.chips.push({ params: ['active'], labelKey: 'filters.reservationActive' });
    }
  }

  /**
   * The URL as it should be after a control moves, built from the fields rather
   * than patched param by param — a filter left out is a filter cleared, and the
   * ones belonging to the other tab are always left out.
   */
  private currentParams(): Record<string, string | null> {
    const shared = {
      tab: this.isRentings ? null : 'reservations',
      car: this.carId === null ? null : String(this.carId),
      client: this.clientId === null ? null : String(this.clientId),
      from: this.route.snapshot.queryParamMap.get('from'),
      to: this.route.snapshot.queryParamMap.get('to')
    };

    if (this.isRentings) {
      return {
        ...shared,
        search: this.search.trim() || null,
        state: enumName(RentingState, this.state),
        dateBasis: enumName(RentingDateBasis, this.dateBasis),
        excludeCancelled: this.excludeCancelled ? 'true' : null
      };
    }

    return {
      ...shared,
      status: enumName(ReservationStatus, this.status),
      active: this.activeOnly ? 'true' : null
    };
  }

  private navigate(params: Record<string, string | null>) {
    applyListFilters(this.router, this.route, params);
  }

  // --- Loading --------------------------------------------------------------

  load() {
    if (this.isRentings) {
      this.loadRentings();
      return;
    }

    this.loadReservations();
  }

  private loadRentings() {
    this.rentings.getRentings(
      this.pageNumber, this.pageSize, this.search.trim() || null,
      this.carId, this.clientId, this.state,
      this.fromDate, this.toDate,
      this.dateBasis ?? undefined, this.excludeCancelled,
      this.sortBy, this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.rows = (result.items || []).map(renting => this.rentingRow(renting));
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  private loadReservations() {
    // The car and the client narrow this tab too. The old reservations screen
    // passed nulls for both, so a "this car's bookings" link only ever answered
    // for hires; on one screen the same chip has to mean the same thing on both
    // halves of it.
    this.reservations.getReservations(
      this.pageNumber, this.pageSize, this.carId, this.clientId, this.status,
      this.fromDate, this.toDate, this.activeOnly,
      this.sortBy, this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.rows = (result.items || []).map(reservation => this.reservationRow(reservation));
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  private rentingRow(renting: RentingDto): BookingRow {
    return {
      kind: 'renting',
      id: renting.id!,
      clientName: renting.clientName,
      carModelName: renting.carModelName,
      carMatricule: renting.carMatricule,
      startDate: renting.startDate,
      endDate: renting.endDate,
      amount: renting.price,
      // Only when there is something left to collect: "0,00 TND due" on every
      // settled hire is noise on the rows that have nothing to say.
      noteAmount: (renting.outstanding?.amount ?? 0) > 0 ? renting.outstanding!.amount : undefined,
      noteCurrency: renting.outstanding?.currency,
      statusLabelKey: this.states.find(s => s.value === renting.rentingState)?.labelKey ?? '',
      tone: this.rentingTone(renting.rentingState),
      // A hire waiting to go out is not the agency's move — the customer has to
      // turn up — and marking every upcoming one would cost the accent bar its
      // meaning (see rule 1 in docs/DESIGN_SYSTEM.md). A car that is late back is.
      needsAction: this.isOverdue(renting),
      overdue: this.isOverdue(renting),
      renting
    };
  }

  private reservationRow(reservation: ReservationDto): BookingRow {
    return {
      kind: 'reservation',
      id: reservation.id!,
      clientName: reservation.clientName,
      carModelName: reservation.carModelName,
      carMatricule: reservation.carMatricule,
      startDate: reservation.startDate,
      endDate: reservation.endDate,
      amount: reservation.price,
      // A hold's figure is how far it has been paid, not what is left: the
      // deposit is the thing being watched, and "120 / 400" says it in one line.
      noteAmount: reservation.price ? (reservation.payedPrice?.amount ?? 0) : undefined,
      noteOf: reservation.price?.amount,
      noteCurrency: reservation.price?.currency,
      statusLabelKey: this.statuses.find(s => s.value === reservation.status)?.labelKey ?? '',
      tone: this.reservationTone(reservation.status),
      // The one row here that is waiting on the agency: unanswered, the hold
      // expires and the car comes free again.
      needsAction: reservation.status === ReservationStatus.PendingConfirmation,
      overdue: false,
      hint: reservation.rejectedReason || reservation.cancelledReason || reservation.expiredReason || undefined,
      reservation
    };
  }

  private rentingTone(state?: RentingState): BookingRow['tone'] {
    switch (state) {
      case RentingState.InProgress: return 'ok';
      case RentingState.NotYet: return 'info';
      case RentingState.Cancelled: return 'danger';
      default: return 'neutral';
    }
  }

  private reservationTone(status?: ReservationStatus): BookingRow['tone'] {
    switch (status) {
      case ReservationStatus.Confirmed:
      case ReservationStatus.Paid: return 'ok';
      case ReservationStatus.Converted: return 'info';
      case ReservationStatus.PendingConfirmation: return 'pending';
      default: return 'neutral';
    }
  }

  /** Running, and the hour it was due back has passed. Compared on the clock the
   *  stored date is on, not the browser's instant (see wallClockNow). */
  private isOverdue(renting: RentingDto): boolean {
    return renting.rentingState === RentingState.InProgress
      && !!renting.endDate && renting.endDate.getTime() < wallClockNow();
  }

  // --- The controls ---------------------------------------------------------

  /** Switching tabs keeps what both halves understand and drops the rest. */
  switchTab(tab: BookingTab) {
    if (this.tab === tab) return;

    this.tab = tab;
    this.search = '';
    this.state = null;
    this.status = null;
    this.dateBasis = null;
    this.excludeCancelled = false;
    this.activeOnly = false;
    this.navigate(this.currentParams());
  }

  onFilter() {
    this.navigate(this.currentParams());
  }

  // Searching goes through the URL too, so the app bar's box and this one are the
  // same control by another name.
  onSearch() {
    this.navigate(this.currentParams());
  }

  clearSearch() {
    this.search = '';
    this.onSearch();
  }

  clearChip(chip: FilterChip) {
    const params = this.currentParams();
    for (const key of chip.params) params[key] = null;
    this.navigate(params);
  }

  sortByKey(key: string) {
    // Clicking the field already sorted on turns it round, the way a table header
    // does; picking a different one starts it at the order that field reads best
    // in, which is newest/highest first for every one of them.
    const current = this.sort[this.tab];
    this.sort[this.tab] = key === current.by
      ? { by: key, direction: current.direction === 'desc' ? 'asc' : 'desc' }
      : { by: key, direction: 'desc' };

    this.pageNumber = 1;
    this.load();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  // --- The panel ------------------------------------------------------------

  /** The whole booking, beside the list it was clicked in. */
  open(row: BookingRow) {
    const data: BookingDetailData = { kind: row.kind, id: row.id };

    this.dialog.open<BookingDetailComponent, BookingDetailData, BookingDetailResult>(
      BookingDetailComponent, {
        data,
        panelClass: 'side-panel',
        position: this.direction.value === 'rtl' ? { top: '0', left: '0' } : { top: '0', right: '0' },
        height: '100vh',
        width: '440px',
        maxWidth: '100vw',
        autoFocus: 'first-tabbable',
        // The panel closes itself on Escape and on a backdrop click, so that it
        // can report what happened while it was open; Material's own handling
        // would close with no result and the list would keep the old statuses.
        disableClose: true
      }).afterClosed().subscribe(result => {
        // A hold that was converted while the panel was open is a hire now, and
        // the hire is what the user asked to see.
        if (result?.rentingId) {
          this.router.navigate(['/renting', result.rentingId]);
          return;
        }

        if (result?.changed) this.load();
      });
  }

  // --- Row actions ----------------------------------------------------------
  // The handful worth doing without opening the booking at all. Everything else
  // is in the panel.

  canPayFor(row: BookingRow): boolean {
    if (!this.canPay) return false;

    if (row.renting) return (row.renting.outstanding?.amount ?? 0) > 0;

    const reservation = row.reservation;
    if (!reservation) return false;

    const convertible = reservation.status === ReservationStatus.Confirmed
      || reservation.status === ReservationStatus.Paid;

    return convertible
      && (reservation.price?.amount ?? 0) - (reservation.payedPrice?.amount ?? 0) > 0;
  }

  pay(row: BookingRow) {
    const outstanding = row.renting
      ? row.renting.outstanding?.amount
      : row.reservation?.price === undefined
        ? undefined
        : (row.reservation.price.amount ?? 0) - (row.reservation.payedPrice?.amount ?? 0);

    this.dialog.open(PaymentDialogComponent, {
      data: {
        target: { kind: row.kind, id: row.id },
        subtitle: [row.clientName, row.carMatricule].filter(Boolean).join(' — '),
        outstanding,
        currency: row.renting?.outstanding?.currency ?? row.amount?.currency
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(recorded => {
      if (recorded) this.load();
    });
  }

  canStart(row: BookingRow): boolean {
    return this.canChangeState && row.renting?.rentingState === RentingState.NotYet;
  }

  startRenting(row: BookingRow) {
    if (row.renting) this.apply(this.actions.startRenting(row.renting));
  }

  canTakeBack(row: BookingRow): boolean {
    return this.canChangeState && row.renting?.rentingState === RentingState.InProgress;
  }

  returnCar(row: BookingRow) {
    if (row.renting) this.apply(this.actions.returnRenting(row.renting));
  }

  isPending(row: BookingRow): boolean {
    return row.reservation?.status === ReservationStatus.PendingConfirmation;
  }

  confirm(row: BookingRow) {
    if (row.reservation) this.apply(this.actions.confirmReservation(row.reservation));
  }

  reject(row: BookingRow) {
    if (row.reservation) this.apply(this.actions.rejectReservation(row.reservation));
  }

  isConvertible(row: BookingRow): boolean {
    return row.reservation?.status === ReservationStatus.Confirmed
      || row.reservation?.status === ReservationStatus.Paid;
  }

  convert(row: BookingRow) {
    if (!row.reservation) return;

    this.actions.convertReservation(row.reservation).subscribe(outcome => {
      if (outcome.rentingId) {
        this.router.navigate(['/renting', outcome.rentingId]);
        return;
      }

      this.handle(outcome);
    });
  }

  canCancel(row: BookingRow): boolean {
    if (row.renting) {
      return row.renting.rentingState === RentingState.NotYet
        || row.renting.rentingState === RentingState.InProgress;
    }

    return this.isPending(row) || this.isConvertible(row);
  }

  // A hire's cancellation decides what happens to the money as well as to the
  // booking, so it opens the dialog that asks (see CancelDialogComponent). A hold
  // has no money rule to settle, so it only asks why.
  cancel(row: BookingRow) {
    if (row.reservation) {
      this.cancelReservation(row.reservation);
      return;
    }

    const renting = row.renting;
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
      if (cancelled) this.load();
    });
  }

  private cancelReservation(reservation: ReservationDto) {
    if (!reservation.id) return;

    const reason = prompt(this.transloco.translate('reservation.promptCancelReason')) ?? undefined;
    if (reason === undefined && !confirm(this.transloco.translate('reservation.confirmCancel'))) return;

    this.errorMessage = '';

    this.reservations.cancelReservation(reservation.id, reason).subscribe({
      next: () => this.load(),
      // The hold moved on while this list was on screen. The row is stale, so say
      // what happened and reload rather than leaving the old status and its
      // now-wrong action buttons up — the same reading BookingActionsService does.
      error: err => this.handle(
        isInvalidTransition(err)
          ? { changed: true, error: this.transloco.translate('reservation.staleState') }
          : { changed: false, error: this.said(err) })
    });
  }

  private said(err: any): string {
    const said = extractValidationErrors(err) ?? extractProblemDetail(err);
    if (!said) console.error(err);
    return said ?? this.transloco.translate('common.actionFailed');
  }

  private apply(action: Observable<BookingActionOutcome>) {
    this.errorMessage = '';
    action.subscribe(outcome => this.handle(outcome));
  }

  private handle(outcome: BookingActionOutcome) {
    if (outcome.error) {
      this.errorMessage = outcome.error;
      setTimeout(() => this.errorMessage = '', 6000);
    }

    if (outcome.changed) this.load();
  }
}
