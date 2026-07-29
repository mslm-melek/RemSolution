export const environment = {
  production: true,

  // See environment.ts: OpenStreetMap's shared tile servers are a development
  // default, not a production one. Swap in the tile provider this deployment
  // pays for, and keep that provider's attribution alongside it.
  mapTileUrl: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  mapTileAttribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',

  // Likewise a development default: Nominatim's usage policy rules out running
  // a product's address lookup against the public instance.
  geocodeSearchUrl: 'https://nominatim.openstreetmap.org/search',
  geocodeReverseUrl: 'https://nominatim.openstreetmap.org/reverse'
};
