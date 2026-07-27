// Language constants and pure resolution helpers.
//
// These live outside the Angular injector because the active language has to be
// known BEFORE the app bootstraps: `<html dir>` decides how Angular Material
// lays itself out (its Directionality reads the document at construction), and
// LOCALE_ID is baked in at bootstrap. Switching language therefore reloads the
// page — see LanguageService.

export type AppLanguage = 'en' | 'fr' | 'ar';

export const SUPPORTED_LANGUAGES: AppLanguage[] = ['en', 'fr', 'ar'];

// French-first: the product's primary market. Also the server's default culture
// (see Program.cs) so untranslated server messages match the UI.
export const DEFAULT_LANGUAGE: AppLanguage = 'fr';

export const RTL_LANGUAGES: AppLanguage[] = ['ar'];

export const LANGUAGE_STORAGE_KEY = 'remsolution.language';

// ASP.NET Core's CookieRequestCultureProvider default cookie. Writing it from
// the SPA is what makes the server-rendered Identity pages (Login, Register,
// password reset) come back in the same language as the SPA.
export const CULTURE_COOKIE = '.AspNetCore.Culture';

export function isSupported(value: string | null | undefined): value is AppLanguage {
  return !!value && (SUPPORTED_LANGUAGES as string[]).includes(value);
}

export function isRtl(language: AppLanguage): boolean {
  return RTL_LANGUAGES.includes(language);
}

// Reads the culture cookie the server (or a previous switch) wrote. Its value
// is url-encoded and shaped "c=fr|uic=fr"; we only care about the UI culture.
function readCultureCookie(): string | null {
  if (typeof document === 'undefined') return null;

  const raw = document.cookie
    .split('; ')
    .find(entry => entry.startsWith(`${CULTURE_COOKIE}=`));
  if (!raw) return null;

  const value = decodeURIComponent(raw.substring(CULTURE_COOKIE.length + 1));
  const uiCulture = value.split('|').find(part => part.startsWith('uic='));
  // Cultures may be regional ("fr-TN"); the app's languages are neutral.
  return uiCulture ? uiCulture.substring(4).split('-')[0] : null;
}

export function writeCultureCookie(language: AppLanguage): void {
  if (typeof document === 'undefined') return;

  const value = encodeURIComponent(`c=${language}|uic=${language}`);
  // One year, root path — same lifetime ASP.NET Core uses by default.
  document.cookie = `${CULTURE_COOKIE}=${value};path=/;max-age=31536000;samesite=lax`;
}

// Resolution order, most to least authoritative:
//  1. the culture cookie — the server seeds it from the signed-in user's stored
//     preference, so it follows the account across devices;
//  2. localStorage — a choice made on this device while signed out;
//  3. the browser's languages;
//  4. the default.
//
// Every browser global is guarded: this runs at module-evaluation time (it
// feeds Transloco's defaultLang and LOCALE_ID in AppModule), and AppModule is
// also imported by AppServerModule, where `document` and `navigator` do not
// exist. Under server rendering the whole chain simply yields the default.
export function resolveLanguage(): AppLanguage {
  const cookie = readCultureCookie();
  if (isSupported(cookie)) return cookie;

  let stored: string | null = null;
  try {
    stored = localStorage.getItem(LANGUAGE_STORAGE_KEY);
  } catch {
    // Private mode, disabled storage, or no window at all: fall through.
  }
  if (isSupported(stored)) return stored;

  if (typeof navigator !== 'undefined') {
    for (const candidate of navigator.languages ?? [navigator.language]) {
      const neutral = candidate?.split('-')[0];
      if (isSupported(neutral)) return neutral;
    }
  }

  return DEFAULT_LANGUAGE;
}

// Applied before bootstrap so Material's Directionality picks the value up.
export function applyDocumentLanguage(language: AppLanguage): void {
  if (typeof document === 'undefined') return;

  const html = document.documentElement;
  html.lang = language;
  html.dir = isRtl(language) ? 'rtl' : 'ltr';
}
