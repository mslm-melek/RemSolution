import { Component, OnDestroy, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute } from '@angular/router';
import { CarsClient, CarDto } from '../web-api-client';
import { ImpersonationService } from '../shared/impersonation.service';

// Read-only browse of an agency's cars for a platform admin. Setting the
// impersonation context makes CarsClient send X-Impersonate-Agency, so the
// tenant-scoped endpoint returns this agency's rows.
@Component({
  selector: 'app-agency-cars',
  templateUrl: './agency-cars.component.html',
  styleUrls: ['./agency.component.css']
})
export class AgencyCarsComponent implements OnInit, OnDestroy {
  agencyId!: number;
  cars: CarDto[] = [];
  displayedColumns: string[] = ['matricule', 'model', 'branch', 'color'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  constructor(
    private route: ActivatedRoute,
    private client: CarsClient,
    private impersonation: ImpersonationService
  ) { }

  ngOnInit() {
    this.agencyId = +this.route.snapshot.paramMap.get('id')!;
    this.impersonation.set(this.agencyId);
    this.load();
  }

  ngOnDestroy() {
    this.impersonation.clear();
  }

  load() {
    this.client.getCars(this.pageNumber, this.pageSize, null, null, null).subscribe({
      next: result => {
        this.cars = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }
}
