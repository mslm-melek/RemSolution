import { Component, OnInit, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgenciesClient, AgencyReliabilityDto, AgencyReportDto
} from '../web-api-client';
import {
  reportKindLabelKey, reportStatusLabelKey, reportStatusTone
} from '../shared/agency-reports';

/**
 * What the marketplace says about this agency, shown back to the agency: the
 * reliability figure customers see, and the complaints behind it. Nobody should
 * learn their own public score from a customer.
 *
 * Read-only on purpose — the agency answers for a report, it does not settle it
 * (see ResolveAgencyReportCommand).
 */
@Component({
  selector: 'app-agency-reputation',
  templateUrl: './agency-reputation.component.html',
  styleUrls: ['./agency-reputation.component.css']
})
export class AgencyReputationComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);

  reliability?: AgencyReliabilityDto;
  reports: AgencyReportDto[] = [];
  loading = true;
  error = '';

  readonly kindLabelKey = reportKindLabelKey;
  readonly statusLabelKey = reportStatusLabelKey;
  readonly statusTone = reportStatusTone;

  constructor(private client: AgenciesClient) { }

  ngOnInit() {
    this.client.getMyAgencyReliability().subscribe({
      next: dto => this.reliability = dto,
      error: () => this.error = this.transloco.translate('reports.loadFailed')
    });

    // One page is enough here: an agency with more than fifty complaints has a
    // problem the screen is not going to help with.
    this.client.getMyAgencyReports(undefined, 1, 50).subscribe({
      next: page => { this.reports = page.items || []; this.loading = false; },
      error: () => {
        this.error = this.transloco.translate('reports.loadFailed');
        this.loading = false;
      }
    });
  }
}
