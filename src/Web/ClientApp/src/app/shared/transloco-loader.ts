import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Translation, TranslocoLoader } from '@jsverse/transloco';

// Runtime JSON loader: one file per language under src/assets/i18n, copied
// verbatim into the build output. Runtime loading (rather than @angular/localize's
// compile-time approach) is what lets a user switch language without a separate
// per-locale build and deployment.
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(lang: string) {
    return this.http.get<Translation>(`/assets/i18n/${lang}.json`);
  }
}
