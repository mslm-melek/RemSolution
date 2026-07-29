// This file can be replaced during build by using the `fileReplacements` array.
// `ng build` replaces `environment.ts` with `environment.prod.ts`.
// The list of file replacements can be found in `angular.json`.

export const environment = {
  production: false,

  // Map tiles for the marketplace map. OpenStreetMap's public tile servers are
  // fine for development and light traffic, but their usage policy rules out a
  // busy production site — point these at a tile provider (or a self-hosted
  // cache) before launch. The attribution is not decoration: ODbL requires the
  // credit to stay on the map, whichever provider serves the tiles.
  mapTileUrl: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  mapTileAttribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',

  // Address lookup for the map picker (agency HQ, branches). Nominatim is
  // OpenStreetMap's own geocoder, so it matches the tiles above; the same
  // caveat applies twice over, because its usage policy is stricter than the
  // tile one — at most one request a second and no bulk use (GeocodingService
  // enforces the rate limit, but not the volume). Point these at a paid
  // geocoder or a self-hosted Nominatim before launch.
  geocodeSearchUrl: 'https://nominatim.openstreetmap.org/search',
  geocodeReverseUrl: 'https://nominatim.openstreetmap.org/reverse'
};

/*
 * For easier debugging in development mode, you can import the following file
 * to ignore zone related error stack frames such as `zone.run`, `zoneDelegate.invokeTask`.
 *
 * This import should be commented out in production mode because it will have a negative impact
 * on performance if an error is thrown.
 */
// import 'zone.js/plugins/zone-error';  // Included with Angular CLI.
