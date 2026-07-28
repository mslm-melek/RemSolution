import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { CarsClient, CarDto, FuelType, ModelCarsClient, ModelCarDto } from '../web-api-client';
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

  fuelTypes = [
    { value: FuelType.Gasoline, labelKey: 'enums.fuelType.gasoline' },
    { value: FuelType.Diesel, labelKey: 'enums.fuelType.diesel' }
  ];

  constructor(private client: CarsClient, private modelCarsClient: ModelCarsClient) { }

  ngOnInit() {
    this.modelCarsClient.getAllModelCars().subscribe({
      next: models => this.models = models || [],
      error: err => console.error(err)
    });

    this.load();
  }

  load() {
    this.client.getCars(
      this.pageNumber,
      this.pageSize,
      this.filterModelId,
      this.filterColor.trim() || null,
      this.filterFuelType,
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

  onFilter() {
    this.pageNumber = 1;
    this.load();
  }

  clearFilters() {
    this.filterModelId = null;
    this.filterColor = '';
    this.filterFuelType = null;
    this.onFilter();
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
