import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { MarketplaceCarDto, MarketplaceClient, MarketplaceDestinationDto } from '../web-api-client';
import { toDateInput } from '../shared/form-utils';
import { PUBLIC_CONTACT } from '../shared/public-site';

// One row of featured cards; a second row would push the board below the fold.
const FEATURED_SIZE = 4;

/**
 * The signed-out landing page: a search that hands over to /browse, the cars
 * on offer, the pick-up places as a departures board, and the pitch to agencies.
 * Every figure on it comes from the public marketplace, so nothing here can
 * promise a car the search will not show.
 */
@Component({
  selector: 'app-public-home',
  templateUrl: './public-home.component.html',
  styleUrls: ['./public-home.component.css']
})
export class PublicHomeComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('board') board?: ElementRef<HTMLElement>;

  featured: MarketplaceCarDto[] = [];
  destinations: MarketplaceDestinationDto[] = [];

  // "c:<countryId>", "b:<branchId>" or '' — one select carries both levels.
  where = '';
  startDate = '';
  endDate = '';

  readonly steps = [
    { icon: 'search', key: 'one' },
    { icon: 'event_available', key: 'two' },
    { icon: 'directions_car', key: 'three' }
  ];

  readonly caps = [
    { icon: 'directions_car', key: 'fleet' },
    { icon: 'event_available', key: 'bookings' },
    { icon: 'receipt_long', key: 'invoices' },
    { icon: 'group', key: 'team' }
  ];

  readonly contactEmail = PUBLIC_CONTACT.email;
  readonly today = new Date();

  private observer?: IntersectionObserver;

  constructor(private client: MarketplaceClient, private router: Router) { }

  ngOnInit() {
    // Same default window as /browse, so the hand-over changes nothing.
    const start = new Date();
    start.setDate(start.getDate() + 1);
    const end = new Date(start);
    end.setDate(end.getDate() + 3);
    this.startDate = toDateInput(start);
    this.endDate = toDateInput(end);

    // Neither list is worth an error banner on a landing page: a section with
    // nothing to show simply is not drawn.
    this.client.getShowcaseCars(FEATURED_SIZE).subscribe({
      next: cars => this.featured = cars || [],
      error: err => console.error(err)
    });

    this.client.getDestinations().subscribe({
      next: destinations => {
        this.destinations = destinations || [];
        // The board is inside an *ngIf that only just became true.
        setTimeout(() => this.watchBoard());
      },
      error: err => console.error(err)
    });
  }

  ngAfterViewInit() {
    this.watchBoard();
  }

  ngOnDestroy() {
    this.observer?.disconnect();
  }

  get carCount(): number {
    return this.destinations.reduce((sum, d) => sum + (d.carCount ?? 0), 0);
  }

  get placeCount(): number {
    return this.destinations.reduce((sum, d) => sum + (d.places?.length ?? 0), 0);
  }

  // Counted from the places, so an agency whose cars all lack a branch is not
  // in it — an undercount, never a promise.
  get agencyCount(): number {
    const ids = new Set<number>();
    this.destinations.forEach(d => d.places?.forEach(p => ids.add(p.agencyId!)));
    return ids.size;
  }

  /** A count as split-flap digits; the index staggers the flip. */
  flaps(count: number | undefined, row: number): { digit: string; i: number }[] {
    return String(count ?? 0).split('').map((digit, k) => ({ digit, i: row + k }));
  }

  search() {
    const [kind, id] = this.where.split(':');
    this.router.navigate(['/browse'], {
      queryParams: {
        country: kind === 'c' ? id : null,
        branch: kind === 'b' ? id : null,
        from: this.startDate || null,
        to: this.endDate || null
      }
    });
  }

  // The counts flip in once, when the board first scrolls into view.
  private watchBoard() {
    const board = this.board?.nativeElement;
    if (!board || this.observer || typeof IntersectionObserver === 'undefined') return;

    this.observer = new IntersectionObserver(entries => {
      if (!entries.some(e => e.isIntersecting)) return;
      board.classList.add('flipping');
      this.observer?.disconnect();
    }, { threshold: 0.3 });
    this.observer.observe(board);
  }
}
