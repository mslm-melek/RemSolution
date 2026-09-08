import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, shareReplay, tap } from 'rxjs/operators';
import { DisplayRateDto, MarketplaceClient, MoneyDto } from '../web-api-client';
import {
  DisplayCurrency, convertAmount, currenciesIn, rateFor,
  resolveDisplayCurrency, storeDisplayCurrency
} from './currency';

/** A price restated in the visitor's currency, with the quote it came from. */
export interface ConvertedPrice {
  amount: number;
  currency: string;
  /** The day the rate was quoted for — a converted price is only as good as its date. */
  asOf?: Date;
}

/**
 * The visitor's display currency, and the rates behind it.
 *
 * Per device and never stored on the account, like the theme: which currency you
 * want to read a price in depends on where you are standing, not on who you are.
 * Unlike the language it needs no reload — nothing is baked in at bootstrap.
 *
 * The rate table is fetched once and shared. It is read from the PUBLIC
 * marketplace endpoint, so this works signed out, which is when most of the
 * browsing happens.
 */
@Injectable({ providedIn: 'root' })
export class CurrencyService {
  private readonly client = inject(MarketplaceClient);

  private readonly currencySubject: BehaviorSubject<DisplayCurrency>;
  private readonly ratesSubject = new BehaviorSubject<DisplayRateDto[]>([]);
  private loading?: Observable<DisplayRateDto[]>;

  constructor() {
    this.currencySubject = new BehaviorSubject<DisplayCurrency>(resolveDisplayCurrency());
  }

  /** What the visitor chose, or null for "leave prices as they are". */
  get current(): DisplayCurrency {
    return this.currencySubject.value;
  }

  get current$(): Observable<DisplayCurrency> {
    return this.currencySubject.asObservable();
  }

  /** Currencies the table can reach. Empty until the rates land, and empty
   *  forever on a platform that has quoted none — the picker then stays away. */
  get available(): string[] {
    return currenciesIn(this.ratesSubject.value);
  }

  get rates$(): Observable<DisplayRateDto[]> {
    return this.ratesSubject.asObservable();
  }

  use(currency: DisplayCurrency): void {
    storeDisplayCurrency(currency);
    this.currencySubject.next(currency);
  }

  /**
   * Loads the rates once per session. Failure is silent by design: a rate table
   * that will not load costs the visitor a convenience, and an error banner over
   * a shop window would cost more than it explains.
   */
  load(): Observable<DisplayRateDto[]> {
    this.loading ??= this.client.getMarketplaceExchangeRates().pipe(
      catchError(() => of([] as DisplayRateDto[])),
      tap(rates => this.ratesSubject.next(rates || [])),
      shareReplay(1)
    );

    return this.loading;
  }

  /**
   * The second line under a price, or null when there is nothing to add: no
   * chosen currency, the price is already in it, or no quote connects the two.
   */
  convert(value: MoneyDto | undefined | null): ConvertedPrice | null {
    const target = this.current;

    if (!target || !value?.currency || value.amount == null) return null;

    const amount = convertAmount(value.amount, value.currency, target, this.ratesSubject.value);
    if (amount === null) return null;

    return {
      amount,
      currency: target,
      asOf: rateFor(value.currency, target, this.ratesSubject.value)?.asOf
    };
  }
}
