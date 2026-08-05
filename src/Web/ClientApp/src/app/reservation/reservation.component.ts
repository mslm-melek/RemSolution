import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { ReservationsClient, ReservationDto, ReservationStatus } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';
import {
  extractProblemDetail, extractValidationErrors, isInvalidTransition
} from '../shared/form-utils';
import {
  FilterChip, applyListFilters, boolParam, dateParam, enumName, enumParam, rangeText,
  withoutParams
} from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';

@Component({
  selector: 'app-reservation',
  templateUrl: './reservation.component.html',
  styleUrls: ['./reservation.component.css']
})
export class ReservationComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  reservations: ReservationDto[] = [];
  // Money can be taken straight from a row (see pay).
  canPay = false;
  displayedColumns: string[] = ['car', 'client', 'period', 'paid', 'status', 'expires', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  error = '';

  // Filters. `status` has a control of its own; the window and "still in play"
  // arrive by link — from the home screen's work queues — and show as removable
  // chips, so the list never quietly shows a subset.
  status: ReservationStatus | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;
  activeOnly = false;
  chips: FilterChip[] = [];

  // Sorting is server-side: the column id doubles as the API's SortBy key, and
  // the starting values mirror the query's own default order (latest first).
  sortBy = 'period';
  sortDirection: SortDirection = 'desc';

  ReservationStatus = ReservationStatus;
  statuses = [
    { value: ReservationStatus.PendingConfirmation, labelKey: 'enums.reservationStatus.pendingConfirmation' },
    { value: ReservationStatus.Confirmed, labelKey: 'enums.reservationStatus.confirmed' },
    { value: ReservationStatus.Paid, labelKey: 'enums.reservationStatus.paid' },
    { value: ReservationStatus.Converted, labelKey: 'enums.reservationStatus.converted' },
    { value: ReservationStatus.Rejected, labelKey: 'enums.reservationStatus.rejected' },
    { value: ReservationStatus.Cancelled, labelKey: 'enums.reservationStatus.cancelled' },
    { value: ReservationStatus.Expired, labelKey: 'enums.reservationStatus.expired' }
  ];

  constructor(
    private client: ReservationsClient,
    private auth: AuthService,
    private actions: BookingActionsService,
    private route: ActivatedRoute,
    private router: Router) { }

  // The filters live in the URL (see shared/list-filters): the dashboard's
  // "requests to review" and the home screen's work queues link here already
  // narrowed to the rows they counted.
  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
    });

    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);
      this.pageNumber = 1;
      this.load();
    });
  }

  private readFilters(params: ParamMap) {
    this.status = enumParam(params, 'status', ReservationStatus) as ReservationStatus | null;
    this.fromDate = dateParam(params, 'from');
    this.toDate = dateParam(params, 'to');
    this.activeOnly = boolParam(params, 'active') === true;

    this.chips = [];

    // The window is on the hold's start date, which is the only one it has that a
    // day is planned by (see GetReservationsWithPaginationQuery).
    if (this.fromDate || this.toDate) {
      const range = rangeText(params.get('from'), params.get('to'));
      this.chips.push({ params: ['from', 'to'], labelKey: 'filters.reservationStarts', labelArgs: { range } });
    }

    if (this.activeOnly) {
      this.chips.push({ params: ['active'], labelKey: 'filters.reservationActive' });
    }
  }

  load() {
    this.client.getReservations(
      this.pageNumber, this.pageSize, null, null, this.status,
      this.fromDate, this.toDate, this.activeOnly,
      this.sortBy, this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.reservations = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  // Filtering goes through the URL; the subscription above reloads the rows. The
  // params that arrived by link are kept — only the control's own is replaced.
  onFilter() {
    applyListFilters(this.router, this.route, {
      ...withoutParams(this.route.snapshot.queryParamMap, ['status']),
      status: enumName(ReservationStatus, this.status)
    });
  }

  clearChip(chip: FilterChip) {
    applyListFilters(
      this.router, this.route, withoutParams(this.route.snapshot.queryParamMap, chip.params));
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  onSort(sort: Sort) {
    this.sortBy = sort.active;
    this.sortDirection = sort.direction || 'asc';
    this.pageNumber = 1;
    this.load();
  }

  // Returns a transloco key; the template pipes it.
  statusLabelKey(status?: ReservationStatus): string {
    return this.statuses.find(s => s.value === status)?.labelKey ?? '';
  }

  statusClass(status?: ReservationStatus): string {
    switch (status) {
      case ReservationStatus.Confirmed:
      case ReservationStatus.Paid: return 'ok';
      case ReservationStatus.Converted: return 'converted';
      case ReservationStatus.PendingConfirmation: return 'pending';
      case ReservationStatus.Rejected:
      case ReservationStatus.Cancelled:
      case ReservationStatus.Expired: return 'ended';
      default: return '';
    }
  }

  // Why the hold left the happy path, for a tooltip on terminal rows.
  statusReason(r: ReservationDto): string {
    return r.rejectedReason || r.cancelledReason || r.expiredReason || '';
  }

  isPending(r: ReservationDto): boolean {
    return r.status === ReservationStatus.PendingConfirmation;
  }

  isConvertible(r: ReservationDto): boolean {
    return r.status === ReservationStatus.Confirmed || r.status === ReservationStatus.Paid;
  }

  isActive(r: ReservationDto): boolean {
    return this.isPending(r) || this.isConvertible(r);
  }

  // Only a confirmed hold with something left to collect: the status rule is the
  // one CreatePaymentCommand enforces, and the price is the ceiling it caps at —
  // so the button never offers a call the server would refuse.
  canPayFor(r: ReservationDto): boolean {
    return this.canPay && this.isConvertible(r) && this.outstandingOf(r) > 0;
  }

  private outstandingOf(r: ReservationDto): number {
    return (r.price?.amount ?? 0) - (r.payedPrice?.amount ?? 0);
  }

  // Takes the deposit or the balance without opening the hold first. The row
  // carries both figures, so the remaining balance is known here.
  pay(r: ReservationDto) {
    if (!r.id) return;

    this.dialog.open(PaymentDialogComponent, {
      data: {
        target: { kind: 'reservation', id: r.id },
        subtitle: [r.clientName, r.carMatricule].filter(Boolean).join(' — '),
        outstanding: r.price === undefined ? undefined : this.outstandingOf(r),
        currency: r.price?.currency
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(recorded => {
      if (recorded) this.load();
    });
  }

  // The lifecycle actions come from BookingActionsService: the home screen's work
  // queues offer the same three, and they have to prompt, refuse and report
  // identically wherever they are clicked.
  confirm(r: ReservationDto) {
    this.apply(this.actions.confirmReservation(r));
  }

  reject(r: ReservationDto) {
    this.apply(this.actions.rejectReservation(r));
  }

  convert(r: ReservationDto) {
    this.actions.convertReservation(r).subscribe(outcome => {
      if (outcome.rentingId) {
        this.router.navigate(['/renting', outcome.rentingId]);
        return;
      }

      this.handle(outcome);
    });
  }

  private apply(action: Observable<BookingActionOutcome>) {
    action.subscribe(outcome => this.handle(outcome));
  }

  private handle(outcome: BookingActionOutcome) {
    if (outcome.error) {
      this.error = outcome.error;
      setTimeout(() => this.error = '', 6000);
    }

    if (outcome.changed) this.load();
  }

  cancel(r: ReservationDto) {
    if (!r.id) return;
    const reason = prompt(this.transloco.translate('reservation.promptCancelReason')) ?? undefined;
    if (reason === undefined && !confirm(this.transloco.translate('reservation.confirmCancel'))) return;
    this.client.cancelReservation(r.id, reason).subscribe({
      next: () => this.load(),
      error: err => this.fail(err)
    });
  }

  // Cancelling stays here — it is the one lifecycle action the home screen's
  // queues do not offer — but it reads a failure the same way the shared actions
  // do (see BookingActionsService).
  private fail(err: any) {
    // The hold moved on while this list was on screen (someone else confirmed or
    // cancelled it). The row is stale, so say what happened and reload rather
    // than leaving the old status and its now-wrong action buttons on screen.
    if (isInvalidTransition(err)) {
      this.error = this.transloco.translate('reservation.staleState');
      this.load();
      setTimeout(() => this.error = '', 6000);
      return;
    }

    const said = extractValidationErrors(err) ?? extractProblemDetail(err);
    if (!said) console.error(err);

    this.error = said ?? this.transloco.translate('common.actionFailed');
    setTimeout(() => this.error = '', 6000);
  }
}
