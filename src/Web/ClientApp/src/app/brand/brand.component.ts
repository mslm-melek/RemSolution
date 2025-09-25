import { Component, ViewChild } from '@angular/core';
import { BrandsClient, BrandDto, CreateBrandCommand } from '../web-api-client';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';

@Component({
  selector: 'app-brand',
  templateUrl: './brand.component.html',
  styleUrls: ['./brand.component.css']
})
export class BrandComponent {
  brands: BrandDto[] = [];
  dataSource = new MatTableDataSource<BrandDto>();
  displayedColumns: string[] = ['name', 'actions'];
  newBrand = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(private client: BrandsClient) {
    this.loadBrands();
  }

  loadBrands() {
    this.client.getBrands().subscribe({
      next: result => {
        this.brands = result || [];
        this.dataSource = new MatTableDataSource(this.brands);
        this.dataSource.paginator = this.paginator;
      },
      error: err => console.error(err)
    });
  }

  addBrand() {
    if (!this.newBrand.trim()) return;
    const command = new CreateBrandCommand({ name: this.newBrand });

    this.client.createBrand(command).subscribe({
      next: () => {
        this.newBrand = '';
        this.loadBrands();
      },
      error: err => console.error(err)
    });
  }

  deleteBrand(brand: BrandDto) {
    // Assuming your BrandDto has an id
    if (!brand.id) return;
    this.client.deleteBrand(brand.id).subscribe({
      next: () => this.loadBrands(),
      error: err => console.error(err)
    });
  }
}
