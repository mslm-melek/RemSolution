import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, timer } from 'rxjs';
import { catchError, concatMap, map } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import { environment } from 'src/environments/environment';

// One candidate address, in the shape the map picker needs.
export interface GeocodeResult {
  label: string;
  latitude: number;
  longitude: number;
}

// Nominatim's response, narrowed to the fields used here.
interface NominatimPlace {
  display_name?: string;
  lat?: string;
  lon?: string;
}

/**
 * Address lookup for the map picker: text → coordinates (search) and
 * coordinates → text (reverse).
 *
 * Called straight from the browser rather than proxied through the API, which is
 * what lets Nominatim see the page's own Referer — its usage policy wants either
 * that or an identifying User-Agent, and a browser will not let script set a
 * User-Agent. Two consequences worth knowing:
 *
 * - The requests carry the `Accept-Language` header the LanguageInterceptor puts
 *   on everything, which is a happy accident: Nominatim honours it, so results
 *   come back in the language the page is in. It is a CORS-safelisted header, so
 *   it triggers no preflight.
 * - The impersonation interceptor keys off `/api/` and so leaves these alone —
 *   an agency id must not travel to a third party.
 *
 * Failures resolve to an empty result rather than an error: a geocoder being
 * down or rate-limiting must not stop someone placing a pin by hand, which is
 * always available.
 */
@Injectable({ providedIn: 'root' })
export class GeocodingService {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);

  // Nominatim allows one request a second per client. Rather than drop calls
  // that arrive early, each is delayed until the slot is free — the picker
  // debounces typing on top of this, so in practice the wait is rare.
  private static readonly MIN_INTERVAL_MS = 1000;
  private nextSlot = 0;

  /** Candidate places for what someone typed. Empty when nothing matches. */
  search(query: string, limit = 5): Observable<GeocodeResult[]> {
    const trimmed = (query || '').trim();

    // Two characters match half the planet; not worth a request.
    if (trimmed.length < 3) return of([]);

    const params = new HttpParams()
      .set('q', trimmed)
      .set('format', 'jsonv2')
      .set('addressdetails', '0')
      .set('limit', String(limit))
      // Nominatim answers in this language when it can, independently of the
      // Accept-Language header, and the two agree here.
      .set('accept-language', this.transloco.getActiveLang());

    return this.throttled(() =>
      this.http.get<NominatimPlace[]>(environment.geocodeSearchUrl, { params })).pipe(
        map(places => (places || []).map(place => this.toResult(place))
          .filter((result): result is GeocodeResult => result !== null)),
        catchError(() => of([]))
      );
  }

  /** The address at a point, or null when the geocoder has no name for it. */
  reverse(latitude: number, longitude: number): Observable<string | null> {
    const params = new HttpParams()
      .set('lat', String(latitude))
      .set('lon', String(longitude))
      .set('format', 'jsonv2')
      .set('accept-language', this.transloco.getActiveLang());

    return this.throttled(() =>
      this.http.get<NominatimPlace>(environment.geocodeReverseUrl, { params })).pipe(
        map(place => place?.display_name?.trim() || null),
        catchError(() => of(null))
      );
  }

  // Defers `request` until at least MIN_INTERVAL_MS after the previous one. The
  // slot is claimed synchronously, so concurrent callers queue instead of
  // all computing the same delay.
  private throttled<T>(request: () => Observable<T>): Observable<T> {
    const now = Date.now();
    const runAt = Math.max(now, this.nextSlot);

    this.nextSlot = runAt + GeocodingService.MIN_INTERVAL_MS;

    return timer(runAt - now).pipe(concatMap(request));
  }

  private toResult(place: NominatimPlace): GeocodeResult | null {
    const latitude = Number(place.lat);
    const longitude = Number(place.lon);

    if (!place.display_name || !Number.isFinite(latitude) || !Number.isFinite(longitude)) {
      return null;
    }

    return { label: place.display_name, latitude, longitude };
  }
}
