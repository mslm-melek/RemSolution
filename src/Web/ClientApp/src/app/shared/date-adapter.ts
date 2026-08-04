// How Material's calendars read and write dates in this app.
//
// The native adapter formats through Intl, which for the Arabic locale prints
// Arabic-Indic digits (٠٤/٠٨/٢٠٢٦) and a locale-specific order — the app has
// decided everywhere else that figures are Latin digits (see AppModule's locale
// registration). So the input box and the calendar's own labels are built here
// from the date parts instead: dd/MM/yyyy in every language, month names still
// translated because those come from Intl.
//
// Typing is accepted in the same order it is shown (dd/MM/yyyy, and dd-MM-yyyy
// or dd.MM.yyyy since keyboards differ), plus the ISO yyyy-MM-dd the API speaks —
// the native adapter would hand "04/08/2026" to Date.parse and read it as an
// American date or as nothing at all.

import { Injectable } from '@angular/core';
import { MatDateFormats, NativeDateAdapter } from '@angular/material/core';

// The format keys are read back in `format()`; they are names rather than Intl
// option objects because every slot below is built by hand.
export const APP_DATE_FORMATS: MatDateFormats = {
  parse: { dateInput: 'input' },
  display: {
    dateInput: 'input',
    monthYearLabel: 'monthYear',
    dateA11yLabel: 'dateA11y',
    monthYearA11yLabel: 'monthYearA11y'
  }
};

@Injectable()
export class AppDateAdapter extends NativeDateAdapter {
  /** Monday: the working week in French and Arabic alike. */
  override getFirstDayOfWeek(): number {
    return 1;
  }

  /** Latin digits in the calendar grid, whatever the language. */
  override getDateNames(): string[] {
    return Array.from({ length: 31 }, (_, i) => String(i + 1));
  }

  override getYearName(date: Date): string {
    return String(date.getFullYear());
  }

  override parse(value: any): Date | null {
    if (value instanceof Date) return this.clone(value);
    if (typeof value === 'number') return new Date(value);
    if (typeof value !== 'string') return null;

    const text = value.trim();
    if (!text) return null;

    // yyyy-MM-dd (what the API and the URL filters use) …
    const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(text);
    if (iso) {
      return this.build(+iso[1], +iso[2], +iso[3]);
    }

    // … and dd/MM/yyyy as shown, in whichever separator was typed.
    const typed = /^(\d{1,2})[/.\-](\d{1,2})[/.\-](\d{2,4})$/.exec(text);
    if (typed) {
      const year = +typed[3];
      return this.build(year < 100 ? 2000 + year : year, +typed[2], +typed[1]);
    }

    return null;
  }

  override format(date: Date, displayFormat: any): string {
    if (!this.isValid(date)) return '';

    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();

    switch (displayFormat) {
      case 'monthYear':
        return `${this.getMonthNames('short')[date.getMonth()]} ${year}`;
      case 'monthYearA11y':
        return `${this.getMonthNames('long')[date.getMonth()]} ${year}`;
      case 'dateA11y':
        return `${date.getDate()} ${this.getMonthNames('long')[date.getMonth()]} ${year}`;
      default:
        return `${day}/${month}/${year}`;
    }
  }

  // Local midnight, like every other date the app builds for the screen (see
  // form-utils): the UTC hop only happens on the way to the API.
  private build(year: number, month: number, day: number): Date | null {
    if (month < 1 || month > 12 || day < 1 || day > 31) return null;

    const date = new Date(year, month - 1, day);

    // Rejects the days that do not exist (31/02 rolls into March otherwise).
    return date.getMonth() === month - 1 && date.getDate() === day ? date : null;
  }
}
