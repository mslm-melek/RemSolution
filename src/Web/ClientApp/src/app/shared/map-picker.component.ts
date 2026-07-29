import {
  AfterViewInit, Component, ElementRef, EventEmitter, Input, NgZone,
  OnChanges, OnDestroy, Output, SimpleChanges, ViewChild, inject
} from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, map, switchMap, takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';
import { GeocodeResult, GeocodingService } from './geocoding.service';
import { environment } from 'src/environments/environment';

// What the picker reports: a coordinate pair, and the address the geocoder gave
// for it when it had one. Both halves are null once the pin is cleared.
export interface PickedLocation {
  latitude: number | null;
  longitude: number | null;
  address: string | null;
}

// Picks a place on a map: search for an address, or click / drag the pin, and
// the reverse-geocoded address comes back with the coordinates. The address is
// a suggestion — the parent owns the field and the user stays free to correct
// it, which matters because geocoders are vague about anything that is not a
// numbered street.
//
// The pin is a divIcon (styled HTML) rather than an image marker, because
// Leaflet's default icons resolve their PNGs by relative URL and break under a
// bundler — the same reason the marketplace map does it.
@Component({
  selector: 'app-map-picker',
  templateUrl: './map-picker.component.html',
  styleUrls: ['./map-picker.component.css']
})
export class MapPickerComponent implements AfterViewInit, OnChanges, OnDestroy {
  private readonly zone = inject(NgZone);
  private readonly geocoding = inject(GeocodingService);

  // static: true is safe — and necessary — because the canvas is a plain child
  // of the host, not inside a structural directive (see the template).
  @ViewChild('canvas', { static: true }) canvas!: ElementRef<HTMLDivElement>;

  @Input() latitude: number | null = null;
  @Input() longitude: number | null = null;

  @Output() picked = new EventEmitter<PickedLocation>();

  // Bound to the search box. Not a form control: the picker sits inside other
  // people's forms and must not add a field to them.
  query = '';
  results: GeocodeResult[] = [];
  searching = false;
  // True once a search came back with nothing, so the box can say so instead of
  // silently showing an empty list.
  noResults = false;

  private map?: L.Map;
  private marker?: L.Marker;
  private readonly queries = new Subject<string>();
  // Where the pin was just put, awaiting an address for it.
  private readonly pinDrops = new Subject<L.LatLngTuple>();
  private readonly destroyed = new Subject<void>();

  // The view to open on when there is no pin yet: roughly Tunisia, matching the
  // marketplace map's own fallback.
  private static readonly FALLBACK_VIEW: L.LatLngTuple = [34.0, 9.0];
  private static readonly FALLBACK_ZOOM = 6;
  private static readonly PIN_ZOOM = 15;

  ngAfterViewInit() {
    // Leaflet fires move/zoom for every animation frame of a pan, so the map
    // lives outside Angular; the handlers below re-enter explicitly when they
    // have something to report.
    this.zone.runOutsideAngular(() => {
      this.map = L.map(this.canvas.nativeElement, {
        // Wheel zoom is armed by a click, so scrolling the form past the map
        // scrolls the page rather than zooming.
        scrollWheelZoom: false,
        attributionControl: true
      }).setView(MapPickerComponent.FALLBACK_VIEW, MapPickerComponent.FALLBACK_ZOOM);

      L.tileLayer(environment.mapTileUrl, {
        maxZoom: 19,
        attribution: environment.mapTileAttribution
      }).addTo(this.map);

      this.map.on('click', () => this.map?.scrollWheelZoom.enable());
      this.map.on('mouseout', () => this.map?.scrollWheelZoom.disable());

      // Clicking the map is the primary way to place the pin.
      this.map.on('click', (event: L.LeafletMouseEvent) =>
        this.zone.run(() => this.moveTo(event.latlng.lat, event.latlng.lng)));
    });

    // switchMap, so moving the pin again abandons the address lookup for where it
    // used to be. Without it a slow earlier reply could land after a faster later
    // one and label the new pin with the old pin's address.
    this.pinDrops.pipe(
      switchMap(([latitude, longitude]) => this.geocoding.reverse(latitude, longitude).pipe(
        map(address => ({ latitude, longitude, address })))),
      takeUntil(this.destroyed)
    ).subscribe(({ latitude, longitude, address }) => {
      if (!address) return;

      this.picked.emit({ latitude, longitude, address });
    });

    // switchMap, so a slower earlier search cannot land on top of a later one.
    this.queries.pipe(
      debounceTime(400),
      switchMap(query => {
        this.searching = true;
        return this.geocoding.search(query);
      }),
      takeUntil(this.destroyed)
    ).subscribe(results => {
      this.searching = false;
      this.results = results;
      this.noResults = results.length === 0 && this.query.trim().length >= 3;
    });

    this.render();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (!this.map) return; // Nothing to draw on yet; ngAfterViewInit will render.

    if (changes['latitude'] || changes['longitude']) this.render();
  }

  ngOnDestroy() {
    this.destroyed.next();
    this.destroyed.complete();
    this.map?.remove();
  }

  // Leaflet measures its container when the map is created. A map inside a tab
  // that was hidden at that moment comes out 0×0, so whoever reveals it calls
  // this — the My agency page does, on tab change.
  refresh() {
    if (!this.map) return;

    this.map.invalidateSize();
    // A fit computed against a 0×0 container means nothing, so re-frame rather
    // than trusting the containment check against those stale bounds.
    this.centre(true);
  }

  onQueryChange(query: string) {
    this.query = query;
    this.noResults = false;

    if (query.trim().length < 3) {
      this.results = [];
      this.searching = false;
      return;
    }

    this.queries.next(query);
  }

  // Picking a candidate takes its address verbatim: it is what the user just
  // chose from the list, so re-deriving it from the coordinates would be both
  // wasteful and less faithful.
  choose(result: GeocodeResult) {
    this.results = [];
    this.query = '';
    this.place(result.latitude, result.longitude);
    this.picked.emit({
      latitude: result.latitude,
      longitude: result.longitude,
      address: result.label
    });
  }

  clear() {
    this.marker?.remove();
    this.marker = undefined;
    // Only the pin is cleared, not the address: someone who typed an address by
    // hand and then removed a misplaced pin should not lose what they typed.
    this.picked.emit({ latitude: null, longitude: null, address: null });
  }

  // Places the pin and asks the geocoder what is there. The coordinates are
  // reported immediately and the address follows in a second emission, so the
  // pin never waits on the network.
  private moveTo(latitude: number, longitude: number) {
    this.place(latitude, longitude);
    this.picked.emit({ latitude, longitude, address: null });
    this.pinDrops.next([latitude, longitude]);
  }

  private render() {
    if (!this.map) return;

    if (this.latitude == null || this.longitude == null) {
      this.marker?.remove();
      this.marker = undefined;
      return;
    }

    this.place(this.latitude, this.longitude);
  }

  private place(latitude: number, longitude: number) {
    if (!this.map) return;

    // Whether this is the pin appearing rather than moving — an existing branch's
    // pin arriving from the API, say. That case has to frame the pin, because the
    // map is still on the wide fallback view it opened with: the pin would
    // otherwise be technically visible but a speck at the edge of a whole region.
    const framing = !this.marker;
    const position: L.LatLngTuple = [latitude, longitude];

    if (this.marker) {
      this.marker.setLatLng(position);
    } else {
      this.marker = L.marker(position, {
        icon: L.divIcon({
          className: 'map-pick-wrap',
          html: '<span class="map-pick"></span>',
          // A zero-size anchor sitting exactly on the coordinate; the pin drawn
          // inside it is lifted above the point by CSS so it does not cover it.
          iconSize: [0, 0],
          iconAnchor: [0, 0]
        }),
        draggable: true,
        keyboard: true
      }).addTo(this.map);

      this.marker.on('dragend', () => {
        const moved = this.marker!.getLatLng();
        this.zone.run(() => this.moveTo(moved.lat, moved.lng));
      });
    }

    this.centre(framing);
  }

  // Brings the pin into view without fighting the user: once the pin is on
  // screen it is left where it is, so a click near the edge does not jump the map
  // out from under the next click. `framing` overrides that for the cases where
  // the view is not the user's own yet — the pin first appearing, or a map being
  // re-measured after its tab was revealed.
  private centre(framing = false) {
    if (!this.map || !this.marker) return;

    const position = this.marker.getLatLng();

    if (!framing && this.map.getBounds().contains(position)) return;

    this.map.setView(position, Math.max(this.map.getZoom(), MapPickerComponent.PIN_ZOOM));
  }
}
