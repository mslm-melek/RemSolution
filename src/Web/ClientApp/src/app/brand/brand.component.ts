import { AfterViewInit, Component, ViewChild, inject } from '@angular/core';
import { BrandsClient, BrandDto, CreateBrandCommand } from '../web-api-client';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-brand',
  templateUrl: './brand.component.html',
  styleUrls: ['./brand.component.css']
})
export class BrandComponent implements AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  brands: BrandDto[] = [];
  dataSource = new MatTableDataSource<BrandDto>();
  displayedColumns: string[] = ['name', 'actions'];
  newBrand = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(private client: BrandsClient) { }

  // The paginator and the sort header only exist once the view is up.
  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnInit() {
    this.loadBrands();
  }

  loadBrands() {
    this.client.getBrands().subscribe({
      next: result => {
        this.brands = result || [];
        // Reuse the same data source so the live paginator/sort bindings survive.
        this.dataSource.data = this.brands;
      },
      error: err => console.error(err)
    });
  }
  errorMessage = '';

  addBrand() {
    if (!this.newBrand.trim()) return;
    this.errorMessage = '';

    const command = new CreateBrandCommand({ name: this.newBrand.trim() });
    this.client.createBrand(command).subscribe({
      next: () => {
        this.newBrand = '';
        this.loadBrands();
        this.errorMessage = '';

      },
      error: (err: any) => {
        if (err.status === 400 && err.error?.errors?.Name) {
          this.errorMessage = err.error.errors.Name[0];
        } else {
          console.error(err);
        }
      }
    });
  }

  deleteBrand(brand: BrandDto) {
    if (!brand.id) return;

    if (confirm(this.transloco.translate('brand.confirmDelete', { name: brand.name }))) {
      this.client.deleteBrand(brand.id).subscribe({
        next: () => this.loadBrands(),
        error: err => console.error(err)
      });
    }
  }
}
