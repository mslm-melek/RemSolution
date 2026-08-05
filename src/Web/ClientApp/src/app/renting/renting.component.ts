import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Observable } from 'rxjs';
import {
  RentingsClient, RentingDto, RentingState, RentingDateBasis, CarsClient, ClientsClient
} from '../web-api-client';
import { BookingActionOutcome, BookingActionsService } from '../shared/booking-actions.service';
import {
  FilterChip, applyListFilters, boolParam, dateParam, enumName, enumParam, idParam, rangeText,
  withoutParams
} from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';
import { CancelDialogComponent } from '../shared/cancel-dialog.component';

@Component({
  selector: 'app-renting',
  templateUrl: './renting.component.html',
  styleUrls: ['./renting.component.css']
})
export class RentingComponent implements OnInit {
  private readonly dialog = inject(MatDialog);
  rentings: RentingDto[] = [];
  displayedColumns: string[] = ['car', 'client', 'period', 'state', 'price', 'actions'];
  errorMessage = '';
  // Money can be taken straight from a row (see pay), and a hire can be started
  // or closed from one (see startRenting / returnCar) — both transitions are the
  // same permission, Renting.Update.
  canPay = false;
  canChangeState = false;

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  // Filters. `state` has a control of its own; the rest arrive by link (from the
  // dashboard's counts, or from a car's / client's history count) and show as
  // removable chips.
  state: RentingState | null = null;
  dateBasis: RentingDateBasis | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;
  excludeCancelled = false;
  // One car's or one client's history. A client matches either seat, so this is
  // "hires this person was on", not only the ones they signed for.
  carId: number | null = null;
  clientId: number | null = null;
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
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private auth: AuthService,
    private actions: BookingActionsService,
    private route: ActivatedRoute,
    private router: Router) { }

  // The URL holds the filters (see shared/list-filters), so the list reloads
  // whenever they change — including when the menu's plain "Rentings" link
  // clears the ones a dashboard tile arrived with.
  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
      this.canChangeState = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
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
      this.carsClient.getCarById(this.carId).subscribe({
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
      this.clientsClient.getClientById(this.clientId).subscribe({
        next: c => chip.labelArgs = {
          client: [c.firstName, c.lastName].filter(Boolean).join(' ') || chip.labelArgs!['client']
        },
        error: () => { }
      });
    }

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
      this.pageNumber, this.pageSize, this.carId, this.clientId, this.state,
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

  // The balance decides, not the state: a finished renting settled late still
  // owes what it owes, and a cancelled one owes its cancellation fee until that
  // is collected (see CancelRentingCommand — a hire cancelled for free reports
  // nothing outstanding, so it drops out of here on its own). A settled row would
  // have any amount refused by the server anyway, the charge being the ceiling.
  canPayFor(renting: RentingDto): boolean {
    return this.canPay && (renting.outstanding?.amount ?? 0) > 0;
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

  // Bringing the car back in without opening the booking first — the same dialog
  // the cars list and the car's page use.
  /** An upcoming hire can go out from the row: the customer is at the counter. */
  canStart(renting: RentingDto): boolean {
    return this.canChangeState && renting.rentingState === RentingState.NotYet && !!renting.id;
  }

  // Handing the car over and taking it back come from BookingActionsService: the
  // home screen's work queues offer the same two, and they have to prompt, refuse
  // and report identically wherever they are clicked.
  startRenting(renting: RentingDto) {
    this.errorMessage = '';
    this.apply(this.actions.startRenting(renting));
  }

  canTakeBack(renting: RentingDto): boolean {
    return this.canChangeState && renting.rentingState === RentingState.InProgress && !!renting.id;
  }

  returnCar(renting: RentingDto) {
    this.apply(this.actions.returnRenting(renting));
  }

  private apply(action: Observable<BookingActionOutcome>) {
    action.subscribe(outcome => {
      if (outcome.error) this.errorMessage = outcome.error;
      if (outcome.changed) this.load();
    });
  }

  canCancel(renting: RentingDto): boolean {
    return renting.rentingState === RentingState.NotYet
      || renting.rentingState === RentingState.InProgress;
  }

  // Cancelling decides what happens to the money as well as to the booking, so it
  // opens the dialog that asks (see CancelDialogComponent) rather than a yes/no box.
  cancelRenting(renting: RentingDto) {
    if (!renting.id) return;

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
}
