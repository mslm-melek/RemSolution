import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { MarketplaceClient, MarketplaceCarDto } from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-marketplace-search',
  templateUrl: './marketplace-search.component.html',
  styleUrls: ['./marketplace-search.component.css']
})
export class MarketplaceSearchComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  startDate = '';
  endDate = '';

  cars: MarketplaceCarDto[] = [];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 12;
  loading = false;
  searched = false;
  error = '';

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    // Sensible default window: tomorrow for 3 days.
    const start = new Date();
    start.setDate(start.getDate() + 1);
    const end = new Date(start);
    end.setDate(end.getDate() + 3);
    this.startDate = toDateInput(start);
    this.endDate = toDateInput(end);
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
    this.searched = true;

    this.client.searchCars(start, end, null, null, null, this.pageNumber, this.pageSize).subscribe({
      next: result => {
        this.cars = result.items || [];
        this.totalCount = result.totalCount || 0;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.error = extractValidationErrors(err) ?? 'Could not search cars. Please try again.';
      }
    });
  }

  onSearchClick() {
    this.pageNumber = 1;
    this.search();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.search();
  }
}
