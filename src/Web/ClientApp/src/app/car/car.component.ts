import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import {
  CarsClient, CarDto, CarStatus, FuelType, ModelCarsClient, ModelCarDto
} from '../web-api-client';
import {
  FilterChip, applyListFilters, boolParam, dateParam, enumName, enumParam, idParam, rangeText,
  withoutParams
} from '../shared/list-filters';
import {
  canRentNow, carAvailability, carAvailabilityClass, carAvailabilityLabelKey
} from '../shared/car-availability';
import { AuthService } from '../shared/auth.service';
import { ReturnDialogComponent } from '../shared/return-dialog.component';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-car',
  templateUrl: './car.component.html',
  styleUrls: ['./car.component.css']
})
export class CarComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  cars: CarDto[] = [];
  models: ModelCarDto[] = [];
  // The photo leads the row: a fleet is recognised by sight before by plate.
  displayedColumns: string[] = [
    'image', 'matricule', 'model', 'status', 'firstCirculationDate', 'color', 'power', 'fuelType',
    'rentings', 'actions'
  ];

  // Hiring out and taking back are the Rentings module's writes, so the row only
  // offers them to someone who could carry them out (the API enforces the same).
  canRent = false;
  canReturn = false;
  canSeeRentings = false;
  // The statistics report, filtered to the row's car — the same entitlement as
  // the dashboard, which is what gates the report itself.
  canSeeStatistics = false;

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  // Sorting is server-side: the column id doubles as the API's SortBy key, and
  // the starting values mirror the query's own default order.
  sortBy = 'matricule';
  sortDirection: SortDirection = 'asc';

  filterModelId: number | null = null;
  filterColor = '';
  filterFuelType: FuelType | null = null;
  // Custody, not administrative status: true = out with a client right now. The
  // dashboard's "on rent" tile links in with it, and the strip now has a control
  // of its own for it (so it is not one of the chips below).
  filterOnRent: boolean | null = null;

  // Filters that arrive by link (from the dashboard's fleet counts) and have no
  // control on the strip; they show as removable chips instead.
  filterStatus: CarStatus | null = null;
  addedFrom: Date | null = null;
  addedTo: Date | null = null;
  chips: FilterChip[] = [];

  fuelTypes = [
    { value: FuelType.Gasoline, labelKey: 'enums.fuelType.gasoline' },
    { value: FuelType.Diesel, labelKey: 'enums.fuelType.diesel' }
  ];

  private static readonly statusLabelKeys: Record<number, string> = {
    [CarStatus.Active]: 'enums.carStatus.active',
    [CarStatus.Maintenance]: 'enums.carStatus.maintenance',
    [CarStatus.Inactive]: 'enums.carStatus.inactive'
  };

  constructor(
    private client: CarsClient,
    private modelCarsClient: ModelCarsClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router) { }

  // The URL holds the filters (see shared/list-filters), so the list reloads
  // whenever they change — including when the menu's plain "Cars" link clears
  // the ones a dashboard tile arrived with.
  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canSeeRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canRent = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');
      this.canReturn = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canSeeStatistics = AuthService.canAccessModule(user, 'Dashboard', 'Dashboard.View');
    });

    this.modelCarsClient.getAllModelCars().subscribe({
      next: models => this.models = models || [],
      error: err => console.error(err)
    });

    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);
      this.pageNumber = 1;
      this.load();
    });
  }

  private readFilters(params: ParamMap) {
    this.filterModelId = idParam(params, 'model');
    this.filterColor = params.get('color') ?? '';
    this.filterFuelType = enumParam(params, 'fuel', FuelType) as FuelType | null;
    this.filterStatus = enumParam(params, 'status', CarStatus) as CarStatus | null;
    this.filterOnRent = boolParam(params, 'onRent');
    this.addedFrom = dateParam(params, 'addedFrom');
    this.addedTo = dateParam(params, 'addedTo');

    this.chips = [];

    if (this.filterStatus !== null) {
      this.chips.push({
        params: ['status'],
        labelKey: 'filters.carStatus',
        labelArgs: { status: this.transloco.translate(CarComponent.statusLabelKeys[this.filterStatus]) }
      });
    }

    if (this.addedFrom || this.addedTo) {
      this.chips.push({
        params: ['addedFrom', 'addedTo'],
        labelKey: 'filters.added',
        labelArgs: { range: rangeText(params.get('addedFrom'), params.get('addedTo')) }
      });
    }
  }

  load() {
    this.client.getCars(
      this.pageNumber,
      this.pageSize,
      this.filterModelId,
      this.filterColor.trim() || null,
      this.filterFuelType,
      this.filterStatus,
      this.filterOnRent,
      this.addedFrom,
      this.addedTo,
      this.sortBy,
      this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.cars = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  // Filtering goes through the URL; the subscription above reloads the rows.
  onFilter() {
    applyListFilters(this.router, this.route, {
      ...withoutParams(this.route.snapshot.queryParamMap, ['model', 'color', 'fuel', 'onRent']),
      model: this.filterModelId,
      color: this.filterColor.trim() || null,
      fuel: enumName(FuelType, this.filterFuelType),
      onRent: this.filterOnRent
    });
  }

  // Clears the chips too: a Clear button that left filters behind would be the
  // very thing the chips exist to prevent.
  clearFilters() {
    applyListFilters(this.router, this.route, {});
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

  // A new sort re-queries from page one: the row that was on top of page three
  // is meaningless once the order changed.
  onSort(sort: Sort) {
    this.sortBy = sort.active;
    this.sortDirection = sort.direction || 'asc';
    this.pageNumber = 1;
    this.load();
  }

  // Returns a transloco key (empty when unset); the template pipes it, so the
  // column re-renders on a language switch.
  fuelTypeLabelKey(value?: FuelType): string {
    return this.fuelTypes.find(f => f.value === value)?.labelKey ?? '';
  }

  // --- Availability, hiring out and taking back -----------------------------
  // The status column answers "can I hire this out right now?", which needs both
  // the administrative status and whether the car is out (see car-availability).

  availabilityLabelKey(car: CarDto): string {
    return carAvailabilityLabelKey(carAvailability(car));
  }

  availabilityClass(car: CarDto): string {
    return carAvailabilityClass(carAvailability(car));
  }

  canRentOut(car: CarDto): boolean {
    return this.canRent && canRentNow(car);
  }

  canTakeBack(car: CarDto): boolean {
    return this.canReturn && !!car.currentRenting?.id;
  }

  // The hire holding the car, closed from here (see ReturnDialogComponent). The
  // list is reloaded rather than patched: the row's status, its history count and
  // possibly the price have all just changed.
  returnCar(car: CarDto) {
    const renting = car.currentRenting;
    if (!renting?.id) return;

    this.dialog.open(ReturnDialogComponent, {
      data: {
        rentingId: renting.id,
        carLabel: [car.matricule, car.modelName].filter(Boolean).join(' · '),
        clientName: renting.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(returned => {
      if (returned) this.load();
    });
  }

  deleteCar(car: CarDto) {
    if (!car.id) return;

    if (confirm(this.transloco.translate('car.confirmDelete', { matricule: car.matricule }))) {
      this.client.deleteCar(car.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
