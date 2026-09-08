import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgencyReportDto, AgencyReportStatus, AgencyReportsClient, ResolveAgencyReportCommand
} from '../web-api-client';
import { extractValidationErrors, extractProblemDetail } from '../shared/form-utils';
import {
  reportKindLabelKey, reportStatusLabelKey, reportStatusTone
} from '../shared/agency-reports';

/**
 * The platform's arbitration queue. Waiting reports first and oldest first (the
 * server orders them), because a complaint left for three weeks is the one that
 * cost the platform its credibility.
 *
 * Everything a decision needs is on the row — the booking, what the agency said
 * when it cancelled, what the customer says — since the reports are
 * platform-level rows and the booking behind one is out of reach from here (see
 * AgencyReport).
 */
@Component({
  selector: 'app-agency-reports',
  templateUrl: './agency-reports.component.html',
  styleUrls: ['./agency-reports.component.css']
})
export class AgencyReportsComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);

  reports: AgencyReportDto[] = [];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 20;
  loading = true;
  error = '';

  /** Waiting first: the queue is a to-do list, so that is what it opens on. */
  filter: AgencyReportStatus | null = AgencyReportStatus.Open;

  /** The report being settled, so only its row shows the form. */
  resolving: AgencyReportDto | null = null;
  note = '';
  saving = false;
  formError = '';

  AgencyReportStatus = AgencyReportStatus;
  readonly kindLabelKey = reportKindLabelKey;
  readonly statusLabelKey = reportStatusLabelKey;
  readonly statusTone = reportStatusTone;

  constructor(private client: AgencyReportsClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;

    this.client.getAgencyReports(this.filter, undefined, this.pageNumber, this.pageSize).subscribe({
      next: page => {
        this.reports = page.items || [];
        this.totalCount = page.totalCount ?? 0;
        this.loading = false;
      },
      error: () => {
        this.error = this.transloco.translate('reports.loadFailed');
        this.loading = false;
      }
    });
  }

  onFilter(status: AgencyReportStatus | null) {
    this.filter = status;
    this.pageNumber = 1;
    this.resolving = null;
    this.load();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  open(report: AgencyReportDto) {
    this.resolving = report;
    this.note = '';
    this.formError = '';
  }

  close() {
    this.resolving = null;
    this.formError = '';
  }

  /** Upholding costs the agency points; dismissing costs nothing. Both need a why. */
  resolve(upheld: boolean) {
    if (!this.resolving?.id) return;

    if (!this.note.trim()) {
      this.formError = this.transloco.translate('reports.noteRequired');
      return;
    }

    this.saving = true;
    this.formError = '';

    const id = this.resolving.id;

    this.client.resolveAgencyReport(id, new ResolveAgencyReportCommand({
      id,
      upheld,
      note: this.note.trim()
    })).subscribe({
      next: () => {
        this.saving = false;
        this.resolving = null;
        // Re-read rather than patched in place: a settled report leaves the
        // "waiting" filter, which is the list on screen.
        this.load();
      },
      error: err => {
        this.saving = false;
        this.formError = extractValidationErrors(err)
          ?? extractProblemDetail(err)
          ?? this.transloco.translate('reports.resolveFailed');
      }
    });
  }
}
