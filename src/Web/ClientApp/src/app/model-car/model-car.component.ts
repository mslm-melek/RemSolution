import { Component, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { ModelCarsClient, ModelCarDto, BrandsClient, BrandDto } from '../web-api-client';

@Component({
  selector: 'app-model-car',
  templateUrl: './model-car.component.html',
  styleUrls: ['./model-car.component.css']
})
export class ModelCarComponent implements OnInit {
  modelCars: ModelCarDto[] = [];
  brands: BrandDto[] = [];
  displayedColumns: string[] = ['name', 'brand', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  filterBrandId: number | null = null;

  constructor(private client: ModelCarsClient, private brandsClient: BrandsClient) { }

  ngOnInit() {
    this.brandsClient.getBrands().subscribe({
      next: brands => this.brands = brands || [],
      error: err => console.error(err)
    });

    this.load();
  }

  load() {
    this.client.getModelCars(this.pageNumber, this.pageSize, this.filterBrandId).subscribe({
      next: result => {
        this.modelCars = result.items || [];
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
    this.filterBrandId = null;
    this.onFilter();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  deleteModelCar(modelCar: ModelCarDto) {
    if (!modelCar.id) return;

    if (confirm(`Delete model "${modelCar.name}"?`)) {
      this.client.deleteModelCar(modelCar.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
