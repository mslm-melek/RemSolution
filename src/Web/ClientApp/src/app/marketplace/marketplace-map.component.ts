import {
  AfterViewInit, Component, ElementRef, EventEmitter, Input, NgZone,
  OnChanges, OnDestroy, Output, SimpleChanges, ViewChild, inject
} from '@angular/core';
import * as L from 'leaflet';
import { MarketplaceMapPointDto } from '../web-api-client';
import { environment } from 'src/environments/environment';

// The viewport the map is looking at, in the shape the search API takes.
export interface MapBounds {
  south: number;
  west: number;
  north: number;
  east: number;
}

// The marketplace map: one price pill per pick-up place, and a "search this
// area" signal when the visitor pans away from what is on screen.
//
// Markers are divIcons (styled HTML) rather than image pins, which is both what
// makes the price readable at a glance and what avoids Leaflet's default marker
// icons — those resolve their PNGs by relative URL and break under a bundler.
@Component({
  selector: 'app-marketplace-map',
  templateUrl: './marketplace-map.component.html',
  styleUrls: ['./marketplace-map.component.css']
})
export class MarketplaceMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  private readonly zone = inject(NgZone);

  // static: true is safe — and necessary — because the canvas is a plain child
  // of the host, not inside a structural directive (see the template).
  @ViewChild('canvas', { static: true }) canvas!: ElementRef<HTMLDivElement>;

  @Input() points: MarketplaceMapPointDto[] = [];
  // The place whose card the visitor is pointing at in the list, so hovering a
  // result lights up its pin. Null clears the highlight.
  @Input() highlightedBranchId: number | null = null;
  // Whether panning offers to re-run the search. Off on the agency page, where
  // the map is a static "here is where we are".
  @Input() searchOnMove = false;

  @Output() placeSelected = new EventEmitter<MarketplaceMapPointDto>();
  @Output() searchArea = new EventEmitter<MapBounds>();

  // Shown after a pan/zoom, until the visitor either searches the area or moves
  // back. A button rather than an automatic refetch: silently replacing results
  // under someone who is still looking at them is disorienting.
  moved = false;

  private map?: L.Map;
  private markers = new Map<number, L.Marker>();
  private layer?: L.LayerGroup;
  // The points that actually have coordinates — what the view is fitted to,
  // kept so a later refresh() can re-fit without re-rendering the markers.
  private placed: MarketplaceMapPointDto[] = [];
  // Set while the component itself is moving the map (the initial fit), so the
  // resulting 'moveend' does not raise the "search this area" prompt.
  private programmaticMove = false;

  ngAfterViewInit() {
    // Leaflet fires a move/zoom event for every animation frame of a pan. Kept
    // outside Angular so a drag does not run change detection dozens of times a
    // second; the handlers below re-enter explicitly when they have something to
    // say.
    this.zone.runOutsideAngular(() => {
      this.map = L.map(this.canvas.nativeElement, {
        // Wheel zoom starts off and is armed by a click (below), so scrolling
        // the results page past the map scrolls the page.
        scrollWheelZoom: false,
        attributionControl: true
      }).setView([34.0, 9.0], 6); // Fallback view; replaced by fitBounds below.

      L.tileLayer(environment.mapTileUrl, {
        maxZoom: 19,
        attribution: environment.mapTileAttribution
      }).addTo(this.map);

      this.map.on('click', () => this.map?.scrollWheelZoom.enable());
      this.map.on('mouseout', () => this.map?.scrollWheelZoom.disable());

      this.map.on('moveend', () => {
        if (!this.searchOnMove || this.programmaticMove) {
          this.programmaticMove = false;
          return;
        }

        this.zone.run(() => this.moved = true);
      });
    });

    this.render();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (!this.map) return; // Nothing to draw on yet; ngAfterViewInit will render.

    if (changes['points']) this.render();
    if (changes['highlightedBranchId']) this.applyHighlight();
  }

  ngOnDestroy() {
    this.map?.remove();
  }

  // Re-runs the search for what is currently on screen.
  searchThisArea() {
    if (!this.map) return;

    this.moved = false;
    const bounds = this.map.getBounds();

    this.searchArea.emit({
      south: bounds.getSouth(),
      west: bounds.getWest(),
      north: bounds.getNorth(),
      east: bounds.getEast()
    });
  }

  // Leaflet measures the container when it is created. A map inside a tab or a
  // panel that was hidden at that moment comes out 0×0, so whoever reveals it
  // calls this. The view is re-fitted as well as re-measured: a fit computed
  // against a 0×0 container produced a zoom level that means nothing.
  refresh() {
    if (!this.map) return;

    this.map.invalidateSize();
    this.fit();
  }

  private render() {
    if (!this.map) return;

    this.layer?.remove();
    this.markers.clear();

    const placed = (this.points || []).filter(p => p.latitude != null && p.longitude != null);
    const layer = L.layerGroup().addTo(this.map);
    this.layer = layer;

    for (const point of placed) {
      const marker = L.marker([point.latitude!, point.longitude!], {
        icon: this.pill(point),
        keyboard: true,
        title: `${point.agencyName} — ${point.branchName}`
      });

      // Back inside Angular: the click drives the result list, which is bound.
      marker.on('click', () => this.zone.run(() => this.placeSelected.emit(point)));
      marker.addTo(layer);
      this.markers.set(point.branchId!, marker);
    }

    this.placed = placed;
    this.fit();

    this.moved = false;
    this.applyHighlight();
  }

  // Frame the pins, but never zoom so far in that a single place fills the map
  // with no context around it.
  private fit() {
    if (!this.map || !this.placed.length) return;

    // Our own move, so it must not raise the "search this area" prompt.
    this.programmaticMove = true;
    this.map.fitBounds(
      L.latLngBounds(this.placed.map(p => [p.latitude!, p.longitude!] as L.LatLngTuple)),
      { padding: [40, 40], maxZoom: 13 }
    );
  }

  private applyHighlight() {
    this.markers.forEach((marker, branchId) => {
      const active = branchId === this.highlightedBranchId;

      marker.getElement()?.classList.toggle('is-highlighted', active);
      // A highlighted pin has to be drawn over its neighbours, not under them.
      marker.setZIndexOffset(active ? 1000 : 0);
    });
  }

  // The pin itself: a price pill, with the car count when a place has several.
  private pill(point: MarketplaceMapPointDto): L.DivIcon {
    const price = point.fromDailyRate
      ? `${Math.round(point.fromDailyRate.amount)} ${point.fromDailyRate.currency}`
      : '—';
    const count = (point.carCount ?? 0) > 1 ? `<span class="pin-count">${point.carCount}</span>` : '';

    return L.divIcon({
      className: 'map-pin-wrap',
      html: `<span class="map-pin">${this.escape(price)}${count}</span>`,
      // Anchored on the middle of the bottom edge so the pill sits above the
      // place rather than off to one side of it.
      iconSize: [0, 0],
      iconAnchor: [0, 0]
    });
  }

  // The pill is raw HTML handed to Leaflet, so anything from the API that lands
  // in it is escaped first.
  private escape(value: string): string {
    return value.replace(/[&<>"']/g, c =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c] as string));
  }
}
