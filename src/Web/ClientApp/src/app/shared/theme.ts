// Theme constants and pure resolution helpers.
//
// These live outside the Angular injector for the same reason the language ones
// do (see language.ts): the theme has to be on `<html>` BEFORE the first paint,
// or every reload shows a white flash before the dark tokens arrive.
//
// Unlike a language switch, a theme switch does NOT reload the page. Every
// colour in the app resolves through a custom property declared on :root and
// :root[data-theme='dark'] — including Angular Material's, which is compiled
// from source in _material-theme.scss — so flipping the attribute is the whole
// operation.

/** What the user chose. 'system' is a real choice, not the absence of one. */
export type ThemeChoice = 'light' | 'dark' | 'system';

/** What actually gets applied. */
export type AppTheme = 'light' | 'dark';

export const THEME_CHOICES: ThemeChoice[] = ['light', 'dark', 'system'];

// Following the OS is the default. The back-office reads best dark and the
// design system is built for it, but silently repainting every existing user's
// app on their next load is not ours to do — their OS already says which they
// want, and the switch is one click away either way.
export const DEFAULT_THEME_CHOICE: ThemeChoice = 'system';

export const THEME_STORAGE_KEY = 'remsolution.theme';

export const DARK_MEDIA_QUERY = '(prefers-color-scheme: dark)';

export function isThemeChoice(value: string | null | undefined): value is ThemeChoice {
  return !!value && (THEME_CHOICES as string[]).includes(value);
}

/** The OS preference, or 'light' where it cannot be read (server rendering). */
export function systemTheme(): AppTheme {
  if (typeof window === 'undefined' || !window.matchMedia) return 'light';
  return window.matchMedia(DARK_MEDIA_QUERY).matches ? 'dark' : 'light';
}

/**
 * The stored choice, or the default. Every browser global is guarded: this runs
 * at module-evaluation time and the module is also reachable from
 * AppServerModule, where `localStorage` does not exist.
 */
export function resolveThemeChoice(): ThemeChoice {
  let stored: string | null = null;
  try {
    stored = localStorage.getItem(THEME_STORAGE_KEY);
  } catch {
    // Private mode, disabled storage, or no window at all: fall through.
  }

  return isThemeChoice(stored) ? stored : DEFAULT_THEME_CHOICE;
}

export function resolveTheme(choice: ThemeChoice = resolveThemeChoice()): AppTheme {
  return choice === 'system' ? systemTheme() : choice;
}

export function storeThemeChoice(choice: ThemeChoice): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, choice);
  } catch {
    // The choice still applies for this page; it just will not survive a reload.
  }
}

/**
 * Light is the absence of the attribute rather than `data-theme='light'`, so the
 * light token block needs no selector of its own and stays the plain :root case.
 */
export function applyDocumentTheme(theme: AppTheme): void {
  if (typeof document === 'undefined') return;

  const html = document.documentElement;
  if (theme === 'dark') {
    html.setAttribute('data-theme', 'dark');
  } else {
    html.removeAttribute('data-theme');
  }
}
