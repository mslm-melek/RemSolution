import { Component, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { CarsClient, CarDto, FuelType, ModelCarsClient, ModelCarDto } from '../web-api-client';

@Component({
  selector: 'app-car',
  templateUrl: './car.component.html',
  styleUrls: ['./car.component.css']
})
export class CarComponent implements OnInit {
  cars: CarDto[] = [];
  models: ModelCarDto[] = [];
  displayedColumns: string[] = ['matricule', 'model', 'firstCirculationDate', 'color', 'power', 'fuelType', 'image', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  filterModelId: number | null = null;
  filterColor = '';
  filterFuelType: FuelType | null = null;

  fuelTypes = [
    { value: FuelType.Gasoline, label: 'Gasoline' },
    { value: FuelType.Diesel, label: 'Diesel' }
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
      this.filterFuelType
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

  fuelTypeLabel(value?: FuelType): string {
    return value === undefined || value === null ? '' : FuelType[value];
  }

  deleteCar(car: CarDto) {
    if (!car.id) return;

    if (confirm(`Delete car "${car.matricule}"?`)) {
      this.client.deleteCar(car.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
