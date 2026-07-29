import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import {
  CarsClient, CarDto, CarStatus, FuelType, ModelCarsClient, ModelCarDto
} from '../web-api-client';
import {
  FilterChip, applyListFilters, boolParam, dateParam, enumName, enumParam, rangeText, withoutParams
} from '../shared/list-filters';
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
  cars: CarDto[] = [];
  models: ModelCarDto[] = [];
  displayedColumns: string[] = ['matricule', 'model', 'firstCirculationDate', 'color', 'power', 'fuelType', 'image', 'actions'];

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

  // Filters that arrive by link (from the dashboard's fleet counts) and have no
  // control on the strip; they show as removable chips instead.
  filterStatus: CarStatus | null = null;
  filterOnRent: boolean | null = null;
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
    private route: ActivatedRoute,
    private router: Router) { }

  // The URL holds the filters (see shared/list-filters), so the list reloads
  // whenever they change — including when the menu's plain "Cars" link clears
  // the ones a dashboard tile arrived with.
  ngOnInit() {
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
    const modelId = Number(params.get('model'));
    this.filterModelId = Number.isInteger(modelId) && modelId > 0 ? modelId : null;
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

    if (this.filterOnRent !== null) {
      this.chips.push({
        params: ['onRent'],
        labelKey: this.filterOnRent ? 'filters.onRent' : 'filters.notOnRent'
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
      ...withoutParams(this.route.snapshot.queryParamMap, ['model', 'color', 'fuel']),
      model: this.filterModelId,
      color: this.filterColor.trim() || null,
      fuel: enumName(FuelType, this.filterFuelType)
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
