import { Injectable } from '@angular/core';
import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';

/**
 * Tags every API call with the active UI language.
 *
 * The server localizes validation failures, plan/booking conflict titles and
 * Identity errors, and the SPA displays that text verbatim (see
 * extractValidationErrors in form-utils.ts). Without this header a signed-out
 * caller — or one whose auth ticket predates a language change — would get
 * server messages in a different language from the surrounding page.
 */
@Injectable()
export class LanguageInterceptor implements HttpInterceptor {
  constructor(private readonly transloco: TranslocoService) { }

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    // The translation files themselves are static assets, not API calls.
    if (request.url.includes('/assets/i18n/')) {
      return next.handle(request);
    }

    return next.handle(request.clone({
      setHeaders: { 'Accept-Language': this.transloco.getActiveLang() }
    }));
  }
}
