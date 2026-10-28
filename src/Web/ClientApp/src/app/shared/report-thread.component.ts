import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import {
  AgencyReportMessageDto, AgencyReportMessageKind, AgencyReportsClient, MarketplaceClient,
  PostAgencyReportMessageCommand, PostMyReportMessageCommand
} from '../web-api-client';
import { extractValidationErrors, extractProblemDetail } from './form-utils';

/** Who is looking: the two parties to the thread, or the agency reading it. */
export type ReportThreadViewer = 'customer' | 'platform' | 'agency';

/**
 * The customer ↔ platform conversation on one report, shown on all three report
 * screens. The two parties can write while the report is open; the agency only
 * reads — it is what is said about it, not a conversation it is part of.
 *
 * Posts through the reader's own endpoint and appends the message in place
 * rather than re-reading the list, which would also reset the parent's filter.
 */
@Component({
  selector: 'app-report-thread',
  templateUrl: './report-thread.component.html',
  styleUrls: ['./report-thread.component.css']
})
export class ReportThreadComponent {
  private readonly transloco = inject(TranslocoService);
  private readonly marketplace = inject(MarketplaceClient);
  private readonly reportsClient = inject(AgencyReportsClient);

  @Input({ required: true }) reportId!: number;
  @Input() messages: AgencyReportMessageDto[] = [];
  @Input({ required: true }) viewer!: ReportThreadViewer;
  /** False once the report is closed: a settled report is frozen. */
  @Input() canWrite = false;
  /** After a message is saved, so the card can clear its "waiting on you" marker. */
  @Output() posted = new EventEmitter<AgencyReportMessageDto>();

  draft = '';
  sending = false;
  error = '';

  get writable(): boolean {
    return this.canWrite && this.viewer !== 'agency';
  }

  AgencyReportMessageKind = AgencyReportMessageKind;

  /**
   * The server says whether the reader wrote it: two platform administrators
   * are both "the platform", but only one of them is "you".
   */
  isMine(message: AgencyReportMessageDto): boolean {
    return !!message.mine;
  }

  /** Withdrawals and reopenings are events on the report, not chat lines. */
  isEvent(message: AgencyReportMessageDto): boolean {
    return message.kind === AgencyReportMessageKind.Withdrawal
      || message.kind === AgencyReportMessageKind.Reopening;
  }

  authorKey(message: AgencyReportMessageDto): string {
    if (this.isMine(message)) return 'reports.thread.you';
    return message.fromPlatform ? 'reports.thread.platform' : 'reports.thread.customer';
  }

  eventKey(message: AgencyReportMessageDto): string {
    const byYou = this.viewer === 'customer';
    return message.kind === AgencyReportMessageKind.Withdrawal
      ? (byYou ? 'reports.thread.withdrawnByYou' : 'reports.thread.withdrawnByCustomer')
      : (byYou ? 'reports.thread.reopenedByYou' : 'reports.thread.reopenedByCustomer');
  }

  send() {
    const body = this.draft.trim();
    if (!body || this.sending) return;

    this.sending = true;
    this.error = '';

    const request = this.viewer === 'platform'
      ? this.reportsClient.postAgencyReportMessage(this.reportId,
          new PostAgencyReportMessageCommand({ id: this.reportId, body }))
      : this.marketplace.postMyReportMessage(this.reportId,
          new PostMyReportMessageCommand({ id: this.reportId, body }));

    request.subscribe({
      next: id => {
        const message = new AgencyReportMessageDto({
          id,
          kind: AgencyReportMessageKind.Message,
          fromPlatform: this.viewer === 'platform',
          mine: true,
          body,
          sentAt: new Date()
        });
        this.messages = [...this.messages, message];
        this.posted.emit(message);
        this.draft = '';
        this.sending = false;
      },
      error: err => {
        this.sending = false;
        this.error = extractValidationErrors(err)
          ?? extractProblemDetail(err)
          ?? this.transloco.translate('reports.thread.sendFailed');
      }
    });
  }
}
