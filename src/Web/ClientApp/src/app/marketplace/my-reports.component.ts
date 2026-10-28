import { Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgencyReportStatus, MarketplaceClient, MyReportDto, ReopenMyReportCommand, WithdrawMyReportCommand
} from '../web-api-client';
import { extractValidationErrors, extractProblemDetail } from '../shared/form-utils';
import {
  reportKindLabelKey, reportStatusLabelKey, reportStatusTone
} from '../shared/agency-reports';

type ReportAction = 'withdraw' | 'reopen';

// The complaints the customer has raised, and what the platform decided. The
// outcome is the whole point of the screen: a report that disappears into the
// platform is worse than no report at all. While one is open they can write to
// the platform on it, or close it themselves with a reason — and take that
// back once.
@Component({
  selector: 'app-my-reports',
  templateUrl: './my-reports.component.html',
  styleUrls: ['./my-reports.component.css']
})
export class MyReportsComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);

  reports: MyReportDto[] = [];
  loading = true;
  error = '';

  /** The report whose reason form is open, and which of the two it is for. */
  acting: { report: MyReportDto, mode: ReportAction } | null = null;
  reason = '';
  saving = false;
  formError = '';

  AgencyReportStatus = AgencyReportStatus;
  readonly kindLabelKey = reportKindLabelKey;
  readonly statusLabelKey = reportStatusLabelKey;
  readonly statusTone = reportStatusTone;

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getMyReports().subscribe({
      next: list => { this.reports = list || []; this.loading = false; },
      error: () => {
        this.error = this.transloco.translate('reports.loadFailed');
        this.loading = false;
      }
    });
  }

  open(report: MyReportDto, mode: ReportAction) {
    this.acting = { report, mode };
    this.reason = '';
    this.formError = '';
  }

  cancel() {
    this.acting = null;
    this.formError = '';
  }

  /** Both need a reason: the platform and the agency read it. */
  submit() {
    if (!this.acting?.report.id) return;

    const { report, mode } = this.acting;
    const reason = this.reason.trim();

    if (!reason) {
      this.formError = this.transloco.translate('reports.customer.reasonRequired');
      return;
    }

    this.saving = true;
    this.formError = '';

    const id = report.id!;
    const request: Observable<void> = mode === 'withdraw'
      ? this.client.withdrawMyReport(id, new WithdrawMyReportCommand({ id, reason }))
      : this.client.reopenMyReport(id, new ReopenMyReportCommand({ id, reason }));

    request.subscribe({
      next: () => {
        this.saving = false;
        this.acting = null;
        this.load();
      },
      error: err => {
        this.saving = false;
        this.formError = extractValidationErrors(err)
          ?? extractProblemDetail(err)
          ?? this.transloco.translate(mode === 'withdraw'
            ? 'reports.customer.withdrawFailed'
            : 'reports.customer.reopenFailed');
      }
    });
  }
}
