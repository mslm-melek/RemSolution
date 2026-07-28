import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { MarketplaceClient, MarketplaceCarDto, MarketplaceDestinationDto, MarketplacePlaceDto } from '../web-api-client';
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

  // Where: a country, and optionally a pick-up place (branch) inside it. Both
  // come from the public destinations lookup, which only lists countries and
  // places that actually have cars on offer.
  destinations: MarketplaceDestinationDto[] = [];
  countryId: number | null = null;
  branchId: number | null = null;

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

    this.client.getDestinations().subscribe({
      next: destinations => this.destinations = destinations || [],
      // The picker is an aid, not the search: losing it must not block browsing.
      error: err => console.error(err)
    });

    this.search();
  }

  // Places offered for the chosen country; with no country chosen, every place,
  // so someone who knows the airport they are flying into can pick it directly.
  get places(): MarketplacePlaceDto[] {
    const countries = this.countryId === null
      ? this.destinations
      : this.destinations.filter(d => d.countryId === this.countryId);

    return countries.reduce<MarketplacePlaceDto[]>(
      (all, destination) => all.concat(destination.places || []), []);
  }

  onCountryChange() {
    // The chosen place may not be in the new country any more.
    if (this.branchId !== null && !this.places.some(p => p.branchId === this.branchId)) {
      this.branchId = null;
    }
    this.onSearchClick();
  }

  // Picking a place implies its country, so the two selects always agree.
  onPlaceChange() {
    if (this.branchId !== null) {
      const owner = this.destinations.find(d =>
        (d.places || []).some(p => p.branchId === this.branchId));
      if (owner) this.countryId = owner.countryId;
    }
    this.onSearchClick();
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

    this.client
      .searchCars(start, end, this.countryId, this.branchId, null, null, this.pageNumber, this.pageSize)
      .subscribe({
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
