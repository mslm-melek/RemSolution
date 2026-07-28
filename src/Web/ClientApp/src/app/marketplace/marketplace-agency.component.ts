import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';
import { MarketplaceAgencyDto, MarketplaceCarDto, MarketplaceClient } from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-marketplace-agency',
  templateUrl: './marketplace-agency.component.html',
  styleUrls: ['./marketplace-agency.component.css']
})
export class MarketplaceAgencyComponent implements OnInit {
  // The error banner is a plain string, so it is translated imperatively rather
  // than through the template pipe.
  private readonly transloco = inject(TranslocoService);

  agency?: MarketplaceAgencyDto;
  notFound = false;
  loadingAgency = true;

  // The agency's fleet for a date window, answered by the same availability
  // query the public search uses — the agency page is a pre-filtered search.
  startDate = '';
  endDate = '';
  branchId: number | null = null;
  cars: MarketplaceCarDto[] = [];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 12;
  loading = false;
  error = '';

  private agencyId = 0;

  constructor(private client: MarketplaceClient, private route: ActivatedRoute) { }

  ngOnInit() {
    this.agencyId = +this.route.snapshot.paramMap.get('id')!;

    // Same default window as /browse, so the two pages agree on what "available"
    // means before anyone touches a date.
    const start = new Date();
    start.setDate(start.getDate() + 1);
    const end = new Date(start);
    end.setDate(end.getDate() + 3);
    this.startDate = toDateInput(start);
    this.endDate = toDateInput(end);

    this.client.getAgency(this.agencyId).subscribe({
      next: agency => { this.agency = agency; this.loadingAgency = false; },
      error: () => { this.notFound = true; this.loadingAgency = false; }
    });

    this.search();
  }

  search() {
    const start = fromDateInput(this.startDate);
    const end = fromDateInput(this.endDate);
    if (!start || !end || end <= start) {
      this.error = this.transloco.translate('marketplace.invalidRange');
      return;
    }
    this.error = '';
    this.loading = true;

    this.client
      .searchCars(start, end, null, this.branchId, null, this.agencyId, this.pageNumber, this.pageSize)
      .subscribe({
        next: result => {
          this.cars = result.items || [];
          this.totalCount = result.totalCount || 0;
          this.loading = false;
        },
        error: err => {
          this.loading = false;
          this.error = extractValidationErrors(err) ?? 'Could not load this agency\'s cars. Please try again.';
        }
      });
  }

  onSearchClick() {
    this.pageNumber = 1;
    this.search();
  }

  onPlaceChange() {
    this.onSearchClick();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.search();
  }
}
