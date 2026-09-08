import { Component, Input, OnChanges, OnDestroy, OnInit, inject } from '@angular/core';
import { Subscription, combineLatest } from 'rxjs';
import { MoneyDto } from '../web-api-client';
import { ConvertedPrice, CurrencyService } from './currency.service';

/**
 * The second line under a price: roughly what it costs in the currency the
 * visitor picked. Renders nothing at all when there is nothing to add — no
 * chosen currency, the price is already in it, or no quote connects the two —
 * so it can be dropped next to any price without a guard around it.
 *
 * Never replaces the price beside it. What the customer will be charged is the
 * agency's own figure, and a converted amount that looked like the price would
 * be a quote the platform cannot honour.
 */
@Component({
  selector: 'app-converted-price',
  template: `
    <span class="converted" *ngIf="converted" [title]="title">
      ≈ {{ converted.amount | number:'1.2-2' }} {{ converted.currency }}
    </span>
  `,
  styles: [`
    .converted {
      display: inline-block;
      font-size: var(--fs-micro);
      color: var(--muted);
      font-variant-numeric: tabular-nums;
      /* Latin-script code and digits: keep the run left-to-right in Arabic. */
      direction: ltr;
      unicode-bidi: isolate;
    }
  `]
})
export class ConvertedPriceComponent implements OnInit, OnChanges, OnDestroy {
  @Input() value?: MoneyDto | null;

  converted: ConvertedPrice | null = null;
  title = '';

  private readonly currency = inject(CurrencyService);
  private subscription?: Subscription;

  ngOnInit() {
    // Both the choice and the table can arrive after the price does.
    this.subscription = combineLatest([this.currency.current$, this.currency.rates$])
      .subscribe(() => this.recalculate());
  }

  ngOnChanges() {
    this.recalculate();
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  private recalculate() {
    this.converted = this.currency.convert(this.value);

    // The date the rate was quoted for, so the figure can be judged. A tooltip
    // rather than a line: it matters when it is questioned, not before.
    this.title = this.converted?.asOf
      ? `${this.converted.asOf.toISOString().slice(0, 10)}`
      : '';
  }
}
