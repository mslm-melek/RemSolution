import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import {
  AppTheme,
  DARK_MEDIA_QUERY,
  THEME_CHOICES,
  ThemeChoice,
  applyDocumentTheme,
  resolveTheme,
  resolveThemeChoice,
  storeThemeChoice,
  systemTheme
} from './theme';

/**
 * Owns the active theme for the session.
 *
 * Unlike LanguageService this never reloads: every colour in the app — the
 * design tokens and Angular Material's own, which is compiled from source in
 * _material-theme.scss — resolves through a custom property on :root, so setting
 * one attribute on <html> repaints everything, overlay panels included.
 *
 * The choice is per-device and not stored on the account. Which theme suits you
 * depends on the screen you are sitting at, so following it across devices would
 * be the wrong behaviour rather than a missing feature.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly available = THEME_CHOICES;

  private readonly choiceSubject: BehaviorSubject<ThemeChoice>;
  private readonly themeSubject: BehaviorSubject<AppTheme>;

  constructor() {
    const choice = resolveThemeChoice();
    this.choiceSubject = new BehaviorSubject<ThemeChoice>(choice);
    this.themeSubject = new BehaviorSubject<AppTheme>(resolveTheme(choice));

    this.watchSystemPreference();
  }

  /** What the user chose, including 'system'. */
  get choice(): ThemeChoice {
    return this.choiceSubject.value;
  }

  /** What is actually on screen. */
  get active(): AppTheme {
    return this.themeSubject.value;
  }

  get choice$(): Observable<ThemeChoice> {
    return this.choiceSubject.asObservable();
  }

  get active$(): Observable<AppTheme> {
    return this.themeSubject.asObservable();
  }

  use(choice: ThemeChoice): void {
    storeThemeChoice(choice);
    this.choiceSubject.next(choice);
    this.apply(resolveTheme(choice));
  }

  /** The toolbar button: straight to the other theme, no menu. */
  toggle(): void {
    this.use(this.active === 'dark' ? 'light' : 'dark');
  }

  private apply(theme: AppTheme): void {
    applyDocumentTheme(theme);
    if (theme !== this.themeSubject.value) {
      this.themeSubject.next(theme);
    }
  }

  // While the choice is 'system' the OS can change under us — a laptop switching
  // at sunset — and the app should follow without a reload.
  private watchSystemPreference(): void {
    if (typeof window === 'undefined' || !window.matchMedia) return;

    window.matchMedia(DARK_MEDIA_QUERY).addEventListener('change', () => {
      if (this.choice === 'system') {
        this.apply(systemTheme());
      }
    });
  }
}
