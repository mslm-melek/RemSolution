import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import {
  ClientNotificationOutcome, NotificationsClient, SendClientLateNoticeCommand
} from '../web-api-client';

/**
 * Sending a client the "your car is overdue" notice, from wherever the agency
 * notices — a row in the client list, or the client's own page.
 *
 * A service because both screens need the same three things and none of them are
 * obvious: confirm first (this writes to a customer in the agency's name), then
 * report what actually happened, because the API answers with an outcome rather
 * than an error — "no email on file" and "already sent today" are things the
 * agency needs told, not failures.
 */
@Injectable({ providedIn: 'root' })
export class LateNoticeService {
  private readonly client = inject(NotificationsClient);
  private readonly transloco = inject(TranslocoService);

  private readonly messages: Record<ClientNotificationOutcome, string> = {
    [ClientNotificationOutcome.Sent]: 'lateNotice.sent',
    [ClientNotificationOutcome.NoEmail]: 'lateNotice.noEmail',
    [ClientNotificationOutcome.AlreadySent]: 'lateNotice.alreadySent',
    // Only reachable if something switches the agency's client mail off between
    // the page loading and the click: the command itself overrides that setting.
    [ClientNotificationOutcome.Disabled]: 'lateNotice.disabled',
    [ClientNotificationOutcome.Failed]: 'lateNotice.failed',
    [ClientNotificationOutcome.NothingToSend]: 'lateNotice.nothingToSend'
  };

  /**
   * Asks first, then sends. Resolves to the message to show the user, or null
   * when they cancelled.
   */
  confirmAndSend(clientName: string, clientId: number, rentingId?: number): Observable<string | null> {
    if (!confirm(this.transloco.translate('lateNotice.confirm', { name: clientName }))) {
      return of(null);
    }

    return this.send(clientId, rentingId);
  }

  send(clientId: number, rentingId?: number): Observable<string> {
    const command = new SendClientLateNoticeCommand({ clientId, rentingId });

    return this.client.sendClientLateNotice(command).pipe(
      map(result => this.describe(result.outcome)),
      catchError(err => {
        console.error(err);
        return of(this.transloco.translate('common.unexpectedError'));
      })
    );
  }

  private describe(outcome: ClientNotificationOutcome | undefined): string {
    const key = outcome === undefined ? undefined : this.messages[outcome];

    return this.transloco.translate(key ?? 'common.unexpectedError');
  }
}
