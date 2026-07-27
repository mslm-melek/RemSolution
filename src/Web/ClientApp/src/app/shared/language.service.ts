import { Injectable } from '@angular/core';
import { take } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import { UsersClient, UpdateMyLanguageCommand } from '../web-api-client';
import { AuthService } from './auth.service';
import {
  AppLanguage,
  DEFAULT_LANGUAGE,
  LANGUAGE_STORAGE_KEY,
  SUPPORTED_LANGUAGES,
  isRtl,
  writeCultureCookie
} from './language';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly available = SUPPORTED_LANGUAGES;

  constructor(
    private readonly transloco: TranslocoService,
    private readonly users: UsersClient,
    private readonly auth: AuthService
  ) { }

  get current(): AppLanguage {
    const active = this.transloco.getActiveLang();
    return (SUPPORTED_LANGUAGES as string[]).includes(active)
      ? active as AppLanguage
      : DEFAULT_LANGUAGE;
  }

  get isRtl(): boolean {
    return isRtl(this.current);
  }

  /**
   * Switches the interface language and reloads the page.
   *
   * The reload is deliberate. Three things are fixed at bootstrap and cannot be
   * re-derived in place: Angular's LOCALE_ID (which drives the date/number
   * pipes used across the tables), Angular Material's Directionality (read from
   * `<html dir>` when the injector is built), and the server-rendered Identity
   * pages, which only see the culture cookie on their next request. Reloading
   * makes all three agree instead of leaving the app half-translated.
   */
  use(language: AppLanguage): void {
    if (language === this.current) return;

    try {
      localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
    } catch {
      // Storage unavailable — the cookie below still carries the choice.
    }

    writeCultureCookie(language);

    // The cookie above is already enough to switch the language. Storing it on
    // the account is the extra step that makes the choice follow the user to
    // another device — and it only applies to signed-in users.
    //
    // The authentication check is load-bearing, not an optimisation: the
    // marketplace is public, and calling an authenticated endpoint anonymously
    // returns 401, which AuthorizeInterceptor turns into a redirect to the login
    // page. An anonymous visitor picking Arabic would get bounced to sign-in.
    this.auth.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user.isAuthenticated) {
        window.location.reload();
        return;
      }

      // A failed save still reloads: the switch must not hinge on the request.
      this.users.updateMyLanguage(new UpdateMyLanguageCommand({ language })).subscribe({
        next: () => window.location.reload(),
        error: () => window.location.reload()
      });
    });
  }
}
