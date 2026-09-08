import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import {
  ExchangeRateDto, ExchangeRatesClient, SetExchangeRateCommand
} from '../web-api-client';
import { extractValidationErrors, extractProblemDetail } from '../shared/form-utils';

/**
 * The platform's display rates: `1 FROM = RATE TO`.
 *
 * These convert nothing that is stored. An agency bills in one currency and its
 * invoices, payments and statistics stay in it; a rate only lets a marketplace
 * visitor read a price in a currency they think in (see the ExchangeRate
 * entity). That is why the screen shows the date each rate is quoted for and
 * nothing else: an approximate figure is honest, an undated one is not.
 *
 * The reverse direction is not entered. It is derived from the pair, so quoting
 * TND → EUR also offers prices to a visitor reading in EUR.
 */
@Component({
  selector: 'app-exchange-rate',
  templateUrl: './exchange-rate.component.html',
  styleUrls: ['./exchange-rate.component.css']
})
export class ExchangeRateComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);

  rates: ExchangeRateDto[] = [];
  loading = true;
  saving = false;
  errorMessage = '';

  form: FormGroup;

  constructor(private fb: FormBuilder, private client: ExchangeRatesClient) {
    this.form = this.fb.group({
      fromCurrency: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
      toCurrency: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
      rate: [null, [Validators.required, Validators.min(0.000001)]],
      // DateFieldComponent's value is a `yyyy-MM-dd` string, not a Date.
      asOf: [today()]
    });
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.client.getExchangeRates().subscribe({
      next: rates => { this.rates = rates || []; this.loading = false; },
      error: () => {
        this.errorMessage = this.transloco.translate('exchangeRate.loadFailed');
        this.loading = false;
      }
    });
  }

  /** Fills the form from a row, so re-quoting a pair is one click and a number. */
  edit(rate: ExchangeRateDto) {
    this.form.patchValue({
      fromCurrency: rate.fromCurrency,
      toCurrency: rate.toCurrency,
      rate: rate.rate,
      asOf: rate.asOf ? rate.asOf.toISOString().slice(0, 10) : today()
    });
  }

  save() {
    if (this.form.invalid || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.form.value;

    // The pair is the identity: the same two codes replace the quote rather than
    // adding a second row (see SetExchangeRateCommand).
    this.client.setExchangeRate(new SetExchangeRateCommand({
      fromCurrency: (v.fromCurrency as string).toUpperCase(),
      toCurrency: (v.toCurrency as string).toUpperCase(),
      rate: Number(v.rate),
      // The picked day, read as that day rather than as a local midnight that
      // slips west of UTC.
      asOf: v.asOf ? new Date(`${v.asOf}T00:00:00Z`) : undefined
    })).subscribe({
      next: () => {
        this.saving = false;
        this.form.reset({ asOf: today() });
        this.load();
      },
      error: err => {
        this.saving = false;
        this.errorMessage = extractValidationErrors(err)
          ?? extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }

  remove(rate: ExchangeRateDto) {
    if (!rate.id) return;

    const question = this.transloco.translate('exchangeRate.confirmDelete', {
      from: rate.fromCurrency, to: rate.toCurrency
    });

    if (!confirm(question)) return;

    this.client.deleteExchangeRate(rate.id).subscribe({
      next: () => this.load(),
      error: err => {
        this.errorMessage = extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}
