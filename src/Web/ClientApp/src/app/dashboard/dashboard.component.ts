import { Component, OnInit, inject } from '@angular/core';
import { DashboardClient, DashboardDto, DashboardMonthPointDto } from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  data?: DashboardDto;
  loading = true;
  errorMessage = '';

  constructor(private client: DashboardClient) { }

  ngOnInit() {
    // Defaults to the current calendar month with six months of history.
    this.client.getDashboard(null, null, 6).subscribe({
      next: data => {
        this.data = data;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        const validationErrors = extractValidationErrors(err);
        this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
        if (!validationErrors) console.error(err);
      }
    });
  }

  // Bars are drawn as a share of the largest value in the series, so the tallest
  // month always fills the chart regardless of the agency's scale.
  private get seriesMax(): number {
    const values: number[] = [];
    for (const point of this.data?.monthlySeries ?? []) {
      values.push(point.collected?.amount ?? 0, point.expenses?.amount ?? 0);
    }
    const max = Math.max(0, ...values);
    return max === 0 ? 1 : max;
  }

  barHeight(value?: number): string {
    return `${Math.round(((value ?? 0) / this.seriesMax) * 100)}%`;
  }

  monthLabel(point: DashboardMonthPointDto): string {
    if (!point.year || !point.month) return '';
    // Month index is 1-based on the wire, 0-based in Date.
    const date = new Date(Date.UTC(point.year, point.month - 1, 1));
    return date.toLocaleDateString(this.transloco.getActiveLang(), { month: 'short', timeZone: 'UTC' });
  }
}
