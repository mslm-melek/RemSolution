import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';
import { applyDocumentLanguage, resolveLanguage } from './app/shared/language';
import { applyDocumentTheme, resolveTheme } from './app/shared/theme';

export function getBaseUrl() {
  return document.getElementsByTagName('base')[0].href;
}

const providers = [
  { provide: 'BASE_URL', useFactory: getBaseUrl, deps: [] }
];

if (environment.production) {
  enableProdMode();
}

// Before bootstrap, not in an APP_INITIALIZER: Angular Material's Directionality
// reads `<html dir>` when the injector is built, so setting it afterwards would
// leave Arabic laid out left-to-right until the next reload.
applyDocumentLanguage(resolveLanguage());

// Also before bootstrap, for a different reason: the dark tokens live on
// `<html data-theme='dark'>`, so setting it any later means the first paint is
// the light theme and every reload flashes white.
applyDocumentTheme(resolveTheme());

platformBrowserDynamic(providers).bootstrapModule(AppModule)
  .catch(err => console.log(err));
