import { Component, OnInit, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { MarketplaceClient, MyReportDto } from '../web-api-client';
import {
  reportKindLabelKey, reportStatusLabelKey, reportStatusTone
} from '../shared/agency-reports';

// The complaints the customer has raised, and what the platform decided. The
// outcome is the whole point of the screen: a report that disappears into the
// platform is worse than no report at all.
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

  readonly kindLabelKey = reportKindLabelKey;
  readonly statusLabelKey = reportStatusLabelKey;
  readonly statusTone = reportStatusTone;

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    this.client.getMyReports().subscribe({
      next: list => { this.reports = list || []; this.loading = false; },
      error: () => {
        this.error = this.transloco.translate('reports.loadFailed');
        this.loading = false;
      }
    });
  }
}
