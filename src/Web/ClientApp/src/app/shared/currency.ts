import { DisplayRateDto } from '../web-api-client';

// The display currency and the arithmetic behind it, in one place.
//
// Conversion is a VIEW concern and happens in the browser: the server never
// sends a converted amount, so no converted amount can be posted back. Every
// price the app shows is still the agency's own — an agency bills in one
// currency, and its invoices, payments and statistics stay in it (see the
// ExchangeRate entity). What this adds is a second line saying roughly what that
// costs in the currency the visitor thinks in.
//
// There is deliberately no rate logic here: the server sends both directions of
// every quoted pair already worked out (see GetDisplayRatesQuery), so all that
// is left is finding the row and multiplying. Which direction applies is the one
// real rule, and it belongs where the test suite runs.

export const DISPLAY_CURRENCY_STORAGE_KEY = 'remsolution.displayCurrency';

/** No preference: prices are shown exactly as the agency quotes them. */
export type DisplayCurrency = string | null;

export function isCurrencyCode(value: string | null | undefined): value is string {
  return !!value && /^[A-Z]{3}$/.test(value);
}

/** The visitor's choice for this device, or null if they have not made one. */
export function resolveDisplayCurrency(): DisplayCurrency {
  let stored: string | null = null;

  try {
    stored = localStorage.getItem(DISPLAY_CURRENCY_STORAGE_KEY);
  } catch {
    // Private mode, disabled storage, or server rendering: no preference.
  }

  return isCurrencyCode(stored) ? stored : null;
}

export function storeDisplayCurrency(currency: DisplayCurrency): void {
  try {
    if (currency) {
      localStorage.setItem(DISPLAY_CURRENCY_STORAGE_KEY, currency);
    } else {
      localStorage.removeItem(DISPLAY_CURRENCY_STORAGE_KEY);
    }
  } catch {
    // As above — the choice simply does not survive the tab.
  }
}

/** Every currency a visitor can ask to read prices in, sorted. */
export function currenciesIn(rates: DisplayRateDto[]): string[] {
  const codes = new Set<string>();

  for (const rate of rates) {
    if (isCurrencyCode(rate.to)) codes.add(rate.to);
  }

  return [...codes].sort();
}

/** The rate applying to a pair, or null when none does. */
export function rateFor(
  from: string, to: string, rates: DisplayRateDto[]): DisplayRateDto | null {
  if (!isCurrencyCode(from) || !isCurrencyCode(to) || from === to) return null;

  return rates.find(r => r.from === from && r.to === to) ?? null;
}

/**
 * `amount` of `from`, expressed in `to` — or null when there is nothing to say:
 * the two are the same currency, or no quote connects them.
 */
export function convertAmount(
  amount: number, from: string, to: string, rates: DisplayRateDto[]): number | null {
  const rate = rateFor(from, to, rates);

  // Two places, like every stored amount. Prices are positive, so the platform's
  // half-up matches the server's away-from-zero.
  return rate?.rate ? Math.round((amount * rate.rate + Number.EPSILON) * 100) / 100 : null;
}
