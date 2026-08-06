import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import {
  CarsClient, CarDto, CarFacetsDto, CarNamedFacetDto, CarStatus, FuelType, ModelCarsClient,
  ModelCarDto
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

/** Cards, or the table. A fleet is recognised by sight, checked by column. */
export type FleetView = 'grid' | 'list';

const VIEW_KEY = 'remsolution.carView';

/** One row of the filter rail: what it stands for, what it is called, how many. */
interface FilterOption<T> {
  value: T;
  /** Transloco key, or — for a branch or a brand — the name the server sent. */
  labelKey?: string;
  label?: string;
  count: number;
}

/** A column the server can order the fleet by, named after its API sort key. */
interface SortOption {
  key: string;
  labelKey: string;
}

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

  // Cards by default: the picture is how a counter clerk finds a car. The choice
  // is remembered, because it is a way of working rather than a per-visit whim.
  view: FleetView = restoreView();

  // The table keeps only the columns worth scanning side by side, so it fits the
  // page instead of scrolling sideways. Colour, power, fuel and the first
  // circulation date are on the car's own page; fuel is still a filter here, and
  // the date is still one of the sort keys below.
  displayedColumns: string[] = [
    'image', 'matricule', 'model', 'branch', 'status', 'dailyRate', 'rentings', 'actions'
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
  pageSize = 12;

  // Sorting is server-side: the column id doubles as the API's SortBy key, and
  // the starting values mirror the query's own default order. The cards have no
  // headers to click, so the toolbar's menu offers the same keys.
  sortBy = 'matricule';
  sortDirection: SortDirection = 'asc';

  readonly sortOptions: SortOption[] = [
    { key: 'matricule', labelKey: 'car.matricule' },
    { key: 'model', labelKey: 'car.model' },
    { key: 'branch', labelKey: 'car.branch' },
    { key: 'status', labelKey: 'common.status' },
    { key: 'dailyRate', labelKey: 'car.dailyRate' },
    { key: 'firstCirculationDate', labelKey: 'car.firstCirculation' },
    { key: 'rentings', labelKey: 'car.rentings' }
  ];

  // Free text over the plate and the model. Lives in the URL like every other
  // filter, so the app bar's search box reaches this list by linking to it.
  search = '';

  filterModelId: number | null = null;
  filterColor = '';
  filterFuelType: FuelType | null = null;
  // Custody, not administrative status: true = out with a client right now. The
  // dashboard's "on rent" tile links in with it, and the rail has a group of its
  // own for it (so it is not one of the chips below).
  filterOnRent: boolean | null = null;
  filterStatus: CarStatus | null = null;
  filterBranchId: number | null = null;
  filterBrandId: number | null = null;

  // Filters that arrive by link (from the dashboard's fleet counts) and have no
  // control of their own; they show as removable chips instead.
  addedFrom: Date | null = null;
  addedTo: Date | null = null;
  chips: FilterChip[] = [];

  // How the fleet divides up, for the counts in the rail. Null until the first
  // answer arrives, which is what keeps the rail from flashing zeroes.
  facets: CarFacetsDto | null = null;

  // The rail is a column beside the list on a desk and a drop-down above it on a
  // phone; this only decides the second case.
  filtersOpen = false;
  // Model and colour are narrow questions, so they stay folded away — unless one
  // of them is what is currently filtering the list (see readFilters).
  moreOpen = false;

  // Branch and brand names, kept as they are seen. A facet only lists options
  // with something in them, so the one that is selected can disappear from the
  // answer that follows it — and a rail row reading "#4" would be a bug on
  // screen. Remembering names costs nothing and keeps the row legible.
  private readonly branchNames = new Map<number, string>();
  private readonly brandNames = new Map<number, string>();

  fuelTypes = [
    { value: FuelType.Gasoline, labelKey: 'enums.fuelType.gasoline' },
    { value: FuelType.Diesel, labelKey: 'enums.fuelType.diesel' }
  ];

  private static readonly statusLabelKeys: Record<number, string> = {
    [CarStatus.Active]: 'enums.carStatus.active',
    [CarStatus.Maintenance]: 'enums.carStatus.maintenance',
    [CarStatus.Inactive]: 'enums.carStatus.inactive'
  };

  private static readonly statusOrder: CarStatus[] = [
    CarStatus.Active, CarStatus.Maintenance, CarStatus.Inactive
  ];

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

    // The model list is only the "more filters" select's options — the rows carry
    // their own model and make (see CarDto.BrandName).
    this.modelCarsClient.getAllModelCars().subscribe({
      next: models => this.models = models || [],
      error: err => console.error(err)
    });

    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);
      this.pageNumber = 1;
      this.load();
      this.loadFacets();
    });
  }

  private readFilters(params: ParamMap) {
    this.search = params.get('search') ?? '';
    this.filterModelId = idParam(params, 'model');
    this.filterColor = params.get('color') ?? '';
    this.filterFuelType = enumParam(params, 'fuel', FuelType) as FuelType | null;
    this.filterStatus = enumParam(params, 'status', CarStatus) as CarStatus | null;
    this.filterOnRent = boolParam(params, 'onRent');
    this.filterBranchId = idParam(params, 'branch');
    this.filterBrandId = idParam(params, 'brand');
    this.addedFrom = dateParam(params, 'addedFrom');
    this.addedTo = dateParam(params, 'addedTo');

    // A filter nobody can see is the thing the chips exist to prevent, and these
    // two live behind a fold.
    if (this.filterModelId !== null || this.filterColor) this.moreOpen = true;

    this.chips = [];

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
      this.search.trim() || null,
      this.filterModelId,
      this.filterColor.trim() || null,
      this.filterFuelType,
      this.filterStatus,
      this.filterBranchId,
      this.filterBrandId,
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

  // The counts beside the filters. Paging does not change them, so this is only
  // called when the filters themselves do.
  private loadFacets() {
    this.client.getCarFacets(
      this.search.trim() || null,
      this.filterModelId,
      this.filterColor.trim() || null,
      this.filterFuelType,
      this.filterStatus,
      this.filterBranchId,
      this.filterBrandId,
      this.filterOnRent,
      this.addedFrom,
      this.addedTo
    ).subscribe({
      next: facets => {
        this.facets = facets;

        for (const branch of facets.branches || []) {
          if (branch.id && branch.name) this.branchNames.set(branch.id, branch.name);
        }

        for (const brand of facets.brands || []) {
          if (brand.id && brand.name) this.brandNames.set(brand.id, brand.name);
        }
      },
      error: err => console.error(err)
    });
  }

  // --- The rail -------------------------------------------------------------

  get statusOptions(): FilterOption<CarStatus>[] {
    return CarComponent.statusOrder.map(status => ({
      value: status,
      labelKey: CarComponent.statusLabelKeys[status],
      count: this.facets?.statuses?.find(f => f.status === status)?.count ?? 0
    }));
  }

  /** In the yard, or out with a client. Two answers to one question. */
  get custodyOptions(): FilterOption<boolean>[] {
    return [
      { value: false, labelKey: 'car.inTheYard', count: this.facets?.inYard ?? 0 },
      { value: true, labelKey: 'car.outOnHire', count: this.facets?.onRent ?? 0 }
    ];
  }

  get branchOptions(): FilterOption<number>[] {
    return this.namedOptions(this.facets?.branches, this.filterBranchId, this.branchNames);
  }

  get brandOptions(): FilterOption<number>[] {
    return this.namedOptions(this.facets?.brands, this.filterBrandId, this.brandNames);
  }

  get fuelOptions(): FilterOption<FuelType>[] {
    return this.fuelTypes.map(fuel => ({
      value: fuel.value,
      labelKey: fuel.labelKey,
      count: this.facets?.fuelTypes?.find(f => f.fuelType === fuel.value)?.count ?? 0
    }));
  }

  /**
   * Branch and brand rows. Options with no id are dropped: "no branch" is a real
   * state a car can be in, but not one the API can be asked for, and a row that
   * filtered nothing when clicked would be worse than no row.
   */
  private namedOptions(
    facets: CarNamedFacetDto[] | undefined,
    selected: number | null,
    names: Map<number, string>
  ): FilterOption<number>[] {
    const options = (facets || [])
      .filter(facet => !!facet.id)
      .map(facet => ({
        value: facet.id!,
        label: facet.name ?? names.get(facet.id!) ?? '',
        count: facet.count ?? 0
      }));

    // The selected option, when the current narrowing has emptied it out.
    if (selected !== null && !options.some(option => option.value === selected)) {
      options.unshift({ value: selected, label: names.get(selected) ?? `#${selected}`, count: 0 });
    }

    return options;
  }

  /** Whether anything is narrowing the list — what the rail's Clear offers. */
  get anyFilter(): boolean {
    return !!this.search || this.filterStatus !== null || this.filterOnRent !== null
      || this.filterBranchId !== null || this.filterBrandId !== null
      || this.filterModelId !== null || !!this.filterColor || this.filterFuelType !== null
      || this.addedFrom !== null || this.addedTo !== null;
  }

  /** For the narrow screen's Filters button: how many groups are answered. */
  get activeFilterCount(): number {
    return [
      this.filterStatus, this.filterOnRent, this.filterBranchId, this.filterBrandId,
      this.filterModelId, this.filterColor || null, this.filterFuelType
    ].filter(value => value !== null && value !== undefined).length;
  }

  // Clicking the row that is already on clears it: one click in, one click out,
  // and no separate "all" row to keep in step with the others.
  toggleStatus(status: CarStatus) {
    this.filterStatus = this.filterStatus === status ? null : status;
    this.apply();
  }

  toggleCustody(onRent: boolean) {
    this.filterOnRent = this.filterOnRent === onRent ? null : onRent;
    this.apply();
  }

  toggleBranch(branchId: number) {
    this.filterBranchId = this.filterBranchId === branchId ? null : branchId;
    this.apply();
  }

  toggleBrand(brandId: number) {
    this.filterBrandId = this.filterBrandId === brandId ? null : brandId;
    this.apply();
  }

  toggleFuel(fuel: FuelType) {
    this.filterFuelType = this.filterFuelType === fuel ? null : fuel;
    this.apply();
  }

  // Filtering goes through the URL; the subscription in ngOnInit reloads the
  // rows and the counts.
  onFilter() {
    this.apply();
  }

  /**
   * Writes every filter this screen has a control for into the URL, leaving the
   * ones it does not (the chips) alone.
   */
  private apply() {
    applyListFilters(this.router, this.route, {
      ...withoutParams(this.route.snapshot.queryParamMap, CarComponent.controlledParams),
      ...this.controlledValues()
    } as Params);
  }

  private static readonly controlledParams = [
    'search', 'model', 'color', 'fuel', 'status', 'onRent', 'branch', 'brand'
  ];

  private controlledValues(): Params {
    return {
      search: this.search.trim() || null,
      model: this.filterModelId,
      color: this.filterColor.trim() || null,
      fuel: enumName(FuelType, this.filterFuelType),
      status: enumName(CarStatus, this.filterStatus),
      onRent: this.filterOnRent,
      branch: this.filterBranchId,
      brand: this.filterBrandId
    };
  }

  // Searching goes through the URL too, so the app bar's box and this one are
  // the same control by another name.
  onSearch() {
    this.apply();
  }

  clearSearch() {
    this.search = '';
    this.onSearch();
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

  // --- View, paging and sorting ---------------------------------------------

  setView(view: FleetView) {
    this.view = view;
    localStorage.setItem(VIEW_KEY, view);
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

  /** The menu the cards sort by: picking the current key flips its direction. */
  sortByKey(key: string) {
    this.onSort({
      active: key,
      direction: this.sortBy === key && this.sortDirection === 'asc' ? 'desc' : 'asc'
    });
  }

  get activeSortLabelKey(): string {
    return this.sortOptions.find(option => option.key === this.sortBy)?.labelKey ?? 'car.matricule';
  }

  /** The make. Off the row itself, so a card names the car before the model
      catalogue has finished loading — and still does for a model that the
      filter's list does not carry. */
  brandOf(car: CarDto): string | null {
    return car.brandName ?? null;
  }

  // --- Availability, hiring out and taking back -----------------------------
  // The status a card or row shows answers "can I hire this out right now?",
  // which needs both the administrative status and whether the car is out (see
  // car-availability).

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
  // possibly the price have all just changed — and so have the counts beside it.
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
      if (returned) {
        this.load();
        this.loadFacets();
      }
    });
  }

  deleteCar(car: CarDto) {
    if (!car.id) return;

    if (confirm(this.transloco.translate('car.confirmDelete', { matricule: car.matricule }))) {
      this.client.deleteCar(car.id).subscribe({
        next: () => {
          this.load();
          this.loadFacets();
        },
        error: err => console.error(err)
      });
    }
  }
}

function restoreView(): FleetView {
  return localStorage.getItem(VIEW_KEY) === 'list' ? 'list' : 'grid';
}
