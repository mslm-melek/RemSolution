import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import {
  RentingsClient, RentingDto, RentingState, RentingDateBasis
} from '../web-api-client';
import {
  FilterChip, applyListFilters, boolParam, dateParam, enumName, enumParam, rangeText, withoutParams
} from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-renting',
  templateUrl: './renting.component.html',
  styleUrls: ['./renting.component.css']
})
export class RentingComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  rentings: RentingDto[] = [];
  displayedColumns: string[] = ['car', 'client', 'period', 'state', 'price', 'actions'];
  // Money can be taken straight from a row (see pay).
  canPay = false;

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  // Filters. `state` has a control of its own; the rest arrive by link (from the
  // dashboard's counts) and show as removable chips.
  state: RentingState | null = null;
  dateBasis: RentingDateBasis | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;
  excludeCancelled = false;
  chips: FilterChip[] = [];

  // Sorting is server-side: the column id doubles as the API's SortBy key, and
  // the starting values mirror the query's own default order (latest first).
  sortBy = 'period';
  sortDirection: SortDirection = 'desc';

  RentingState = RentingState;
  states = [
    { value: RentingState.NotYet, labelKey: 'enums.rentingState.notYet' },
    { value: RentingState.InProgress, labelKey: 'enums.rentingState.inProgress' },
    { value: RentingState.Done, labelKey: 'enums.rentingState.done' },
    { value: RentingState.Cancelled, labelKey: 'enums.rentingState.cancelled' }
  ];

  constructor(
    private client: RentingsClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router) { }

  // The URL holds the filters (see shared/list-filters), so the list reloads
  // whenever they change — including when the menu's plain "Rentings" link
  // clears the ones a dashboard tile arrived with.
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
    this.state = enumParam(params, 'state', RentingState) as RentingState | null;
    this.dateBasis = enumParam(params, 'dateBasis', RentingDateBasis) as RentingDateBasis | null;
    this.fromDate = dateParam(params, 'from');
    this.toDate = dateParam(params, 'to');
    this.excludeCancelled = boolParam(params, 'excludeCancelled') === true;

    this.chips = [];

    if (this.fromDate || this.toDate) {
      const range = rangeText(params.get('from'), params.get('to'));
      const labelKey = this.dateBasis === RentingDateBasis.Starts ? 'filters.rentingStarts'
        : this.dateBasis === RentingDateBasis.Ends ? 'filters.rentingEnds'
          : 'filters.rentingRuns';
      this.chips.push({ params: ['from', 'to', 'dateBasis'], labelKey, labelArgs: { range } });
    }

    if (this.excludeCancelled) {
      this.chips.push({ params: ['excludeCancelled'], labelKey: 'filters.excludingCancelled' });
    }
  }

  load() {
    this.client.getRentings(
      this.pageNumber, this.pageSize, null, null, this.state,
      this.fromDate, this.toDate,
      this.dateBasis ?? undefined, this.excludeCancelled,
      this.sortBy, this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.rentings = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  // Filtering goes through the URL; the subscription above reloads the rows.
  onFilter() {
    applyListFilters(this.router, this.route, {
      ...withoutParams(this.route.snapshot.queryParamMap, ['state']),
      state: enumName(RentingState, this.state)
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
  stateLabelKey(state?: RentingState): string {
    return this.states.find(s => s.value === state)?.labelKey ?? '';
  }

  // Chip tone for the state column: running now, still to come, or finished.
  stateClass(state?: RentingState): string {
    switch (state) {
      case RentingState.InProgress: return 'ok';
      case RentingState.NotYet: return 'info';
      case RentingState.Cancelled: return 'danger';
      default: return 'neutral';
    }
  }

  // Only a renting that still owes money is collected on: a cancelled one is
  // never paid, and a settled one would have any amount refused by the server
  // anyway (the price is the ceiling). A finished renting settled late still
  // qualifies — what matters is the balance, not the state.
  canPayFor(renting: RentingDto): boolean {
    return this.canPay
      && renting.rentingState !== RentingState.Cancelled
      && (renting.outstanding?.amount ?? 0) > 0;
  }

  // Takes the money without opening the booking first.
  pay(renting: RentingDto) {
    if (!renting.id) return;

    this.dialog.open(PaymentDialogComponent, {
      data: {
        target: { kind: 'renting', id: renting.id },
        subtitle: [renting.clientName, renting.carMatricule].filter(Boolean).join(' — '),
        outstanding: renting.outstanding?.amount,
        currency: renting.outstanding?.currency ?? renting.price?.currency
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(recorded => {
      if (recorded) this.load();
    });
  }

  canCancel(renting: RentingDto): boolean {
    return renting.rentingState === RentingState.NotYet
      || renting.rentingState === RentingState.InProgress;
  }

  cancelRenting(renting: RentingDto) {
    if (!renting.id) return;
    if (confirm(this.transloco.translate('renting.confirmCancel'))) {
      this.client.cancelRenting(renting.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
