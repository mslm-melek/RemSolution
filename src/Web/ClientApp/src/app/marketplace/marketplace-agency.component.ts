import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgencyReviewDto, MarketplaceAgencyDto, MarketplaceCarDto, MarketplaceClient,
  MarketplaceMapPointDto
} from '../web-api-client';
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
  placePoints: MarketplaceMapPointDto[] = [];

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

  // What people said. Loaded page by page behind a "show more" rather than all
  // at once: a well-reviewed agency has hundreds and the shopfront is not a
  // review site.
  reviews: AgencyReviewDto[] = [];
  reviewsTotal = 0;
  reviewsPage = 1;
  loadingReviews = false;
  private readonly reviewsPageSize = 5;

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
      next: agency => {
        this.agency = agency;
        this.placePoints = this.buildPlacePoints(agency);
        this.loadingAgency = false;
      },
      error: () => { this.notFound = true; this.loadingAgency = false; }
    });

    this.loadReviews();
    this.search();
  }

  // The agency's pick-up places as map pins. Built from the shopfront payload
  // rather than fetched: the places are already known, and this map is a "here
  // is where we are", not a search.
  //
  // Materialised into a field instead of computed by a getter: the map redraws
  // whenever this input changes identity, and a getter would hand it a brand new
  // array on every change-detection pass — re-fitting the view continuously.
  private buildPlacePoints(agency: MarketplaceAgencyDto): MarketplaceMapPointDto[] {
    return (agency.places || [])
      .filter(place => place.latitude != null && place.longitude != null)
      .map(place => new MarketplaceMapPointDto({
        branchId: place.branchId,
        branchName: place.name,
        agencyId: place.agencyId,
        agencyName: place.agencyName,
        latitude: place.latitude,
        longitude: place.longitude,
        carCount: place.carCount,
        // The pin shows the agency's entry price; a per-place cheapest would
        // need a second query for something nobody compares within one agency.
        fromDailyRate: agency.fromDailyRate,
        agencyRating: agency.rating?.averageRating,
        agencyReviewCount: agency.rating?.reviewCount
      }));
  }

  // Percentage width of a star's bar in the breakdown, relative to the most
  // common rating rather than the total: with 90% five-star reviews, bars
  // scaled to the total would render every other row as an invisible sliver.
  breakdownWidth(count: number): number {
    const counts = this.agency?.rating?.counts || [];
    const peak = Math.max(1, ...counts);

    return (count / peak) * 100;
  }

  // Newest first, five at a time — the list appends rather than replaces, so
  // "show more" reads as more rather than as a page change.
  loadReviews() {
    this.loadingReviews = true;

    this.client.getAgencyReviews(this.agencyId, this.reviewsPage, this.reviewsPageSize).subscribe({
      next: result => {
        this.reviews = this.reviews.concat(result.items || []);
        this.reviewsTotal = result.totalCount || 0;
        this.loadingReviews = false;
      },
      // Reviews are a section of the page, not the page: a failure here must
      // not take the fleet down with it.
      error: err => { this.loadingReviews = false; console.error(err); }
    });
  }

  showMoreReviews() {
    this.reviewsPage += 1;
    this.loadReviews();
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
      // No viewport: this page is already scoped to one agency, and its map is
      // a "here is where we are" rather than a search filter.
      .searchCars(
        start, end, null, this.branchId, null, this.agencyId,
        null, null, null, null, this.pageNumber, this.pageSize)
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
