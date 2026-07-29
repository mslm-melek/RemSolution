import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import {
  MarketplaceClient, MarketplaceCarDto, MarketplaceDestinationDto,
  MarketplaceMapPointDto, MarketplacePlaceDto
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';
import { MapBounds, MarketplaceMapComponent } from './marketplace-map.component';

export type SearchView = 'list' | 'map';

@Component({
  selector: 'app-marketplace-search',
  templateUrl: './marketplace-search.component.html',
  styleUrls: ['./marketplace-search.component.css']
})
export class MarketplaceSearchComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);

  // Only present while the map view is on (*ngIf), which is also why every use
  // is optional-chained.
  @ViewChild(MarketplaceMapComponent) map?: MarketplaceMapComponent;

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

  view: SearchView = 'list';
  mapPoints: MarketplaceMapPointDto[] = [];
  // The place under the pointer in the results list, lighting up its pin.
  highlightedBranchId: number | null = null;
  // Set by "search this area". Applied to BOTH the list and the map, so the two
  // never describe different sets of cars. Cleared whenever the visitor changes
  // a filter, otherwise a viewport they have forgotten about keeps silently
  // narrowing their results.
  private bounds: MapBounds | null = null;

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

  setView(view: SearchView) {
    if (this.view === view) return;

    this.view = view;

    if (view === 'list') {
      // Leaving the map drops the viewport with it: a filter the visitor can no
      // longer see must not keep narrowing the list.
      this.bounds = null;
      this.search();
      return;
    }

    this.loadMapPoints();
    // The map is created inside an *ngIf that has only just become true, so it
    // measures its container on the next turn — before Angular has laid the
    // panel out, Leaflet would size it 0×0.
    setTimeout(() => this.map?.refresh());
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
      .searchCars(
        start, end, this.countryId, this.branchId, null, null,
        this.bounds?.south ?? null, this.bounds?.west ?? null,
        this.bounds?.north ?? null, this.bounds?.east ?? null,
        this.pageNumber, this.pageSize)
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
    // A new search is a new intent; the old viewport does not survive it.
    this.bounds = null;
    this.search();
    if (this.view === 'map') this.loadMapPoints();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.search();
  }

  // "Search this area": the viewport becomes a filter on both halves.
  onSearchArea(bounds: MapBounds) {
    this.bounds = bounds;
    this.pageNumber = 1;
    this.search();
    this.loadMapPoints();
  }

  // Clicking a pin narrows to that pick-up place — the same filter the place
  // select drives, so "Any place" is how you get back out.
  onPlaceSelected(point: MarketplaceMapPointDto) {
    this.branchId = point.branchId ?? null;
    this.onPlaceChange();
  }

  private loadMapPoints() {
    const start = fromDateInput(this.startDate);
    const end = fromDateInput(this.endDate);
    if (!start || !end || end <= start) return;

    this.client
      .searchCarsOnMap(
        start, end, this.countryId, this.branchId, null, null,
        this.bounds?.south ?? null, this.bounds?.west ?? null,
        this.bounds?.north ?? null, this.bounds?.east ?? null)
      .subscribe({
        next: points => this.mapPoints = points || [],
        // The list is the search; a map that fails to load must not take the
        // results down with it.
        error: err => console.error(err)
      });
  }
}
