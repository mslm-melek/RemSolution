import { Component, LOCALE_ID, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import {
  MarkNotificationsReadCommand, NotificationDto, NotificationKind, NotificationsClient
} from '../web-api-client';
import { NotificationBadgeService } from '../shared/notification-badge.service';
import {
  NotificationLook, notificationArgs, notificationLook, notificationMessageKey
} from '../shared/notifications';
import { applyListFilters, boolParam, enumName, enumParam } from '../shared/list-filters';
import { extractValidationErrors } from '../shared/form-utils';

const PAGE_SIZE = 20;

/**
 * The signed-in user's notifications: what the agency needs to act on, newest
 * first.
 *
 * Rows are not rendered from stored text. Each carries a message key and its
 * arguments, so the sentence is built here through Transloco — which is what lets
 * the same notification read in French today and in Arabic after the user
 * switches language (see shared/notifications.ts).
 */
@Component({
  selector: 'app-notification',
  templateUrl: './notification.component.html',
  styleUrls: ['./notification.component.css']
})
export class NotificationComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  private readonly locale = inject(LOCALE_ID);
  private readonly badge = inject(NotificationBadgeService);

  notifications: NotificationDto[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = PAGE_SIZE;
  loading = false;
  errorMessage = '';

  // Filters, held in the URL like every other list (see shared/list-filters): the
  // bell links here with ?unread, so the badge and the screen it opens agree.
  onlyUnread = false;
  kind: NotificationKind | null = null;

  // Offered as filter options in the order they matter to an agency.
  readonly kinds: NotificationKind[] = [
    NotificationKind.RentingOverdue,
    NotificationKind.CarExpenseDue,
    NotificationKind.ReservationUpcoming
  ];

  NotificationKind = NotificationKind;

  constructor(
    private client: NotificationsClient,
    private route: ActivatedRoute,
    private router: Router) { }

  ngOnInit() {
    this.route.queryParamMap.subscribe(params => {
      this.onlyUnread = boolParam(params, 'unread') === true;
      this.kind = enumParam(params, 'kind', NotificationKind) as NotificationKind | null;
      // A filter change re-reads from the first page: page 3 of the old filter is
      // rarely page 3 of the new one.
      this.pageIndex = 0;
      this.load();
    });
  }

  load() {
    this.loading = true;

    this.client.getMine(this.pageIndex + 1, this.pageSize, this.onlyUnread, this.kind).subscribe({
      next: page => {
        this.loading = false;
        this.notifications = page.items ?? [];
        this.totalCount = page.totalCount ?? 0;
      },
      error: err => {
        this.loading = false;
        this.handleError(err);
      }
    });
  }

  onPage(event: { pageIndex: number; pageSize: number }) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  // Filters travel through the URL; the subscription above reloads the rows.
  onFilter() {
    applyListFilters(this.router, this.route, {
      unread: this.onlyUnread ? 'true' : null,
      kind: enumName(NotificationKind, this.kind)
    });
  }

  look(notification: NotificationDto): NotificationLook {
    return notificationLook(notification.kind);
  }

  /** The rendered sentence for a row. */
  text(notification: NotificationDto): string {
    return this.transloco.translate(
      notificationMessageKey(notification),
      notificationArgs(notification, this.locale));
  }

  /**
   * Opens what the notification is about, marking it read on the way. Reading by
   * following the link is the common case, so it needs no separate click.
   */
  open(notification: NotificationDto) {
    const link = notification.link;

    this.markRead([notification], () => {
      if (link) this.router.navigateByUrl(link);
    });
  }

  markOneRead(notification: NotificationDto) {
    this.markRead([notification]);
  }

  /** Clears the badge in one go — including rows on pages not on screen. */
  markAllRead() {
    if (!this.totalCount) return;

    // No ids: the command reads that as "every unread one of mine" (see
    // MarkNotificationsReadCommand), which is what this button promises.
    this.send(new MarkNotificationsReadCommand());
  }

  private markRead(notifications: NotificationDto[], then?: () => void) {
    const ids = notifications
      .filter(notification => !notification.isRead && notification.id)
      .map(notification => notification.id!);

    if (!ids.length) {
      then?.();
      return;
    }

    this.send(new MarkNotificationsReadCommand({ ids }), then);
  }

  private send(command: MarkNotificationsReadCommand, then?: () => void) {
    this.client.markNotificationsRead(command).subscribe({
      next: marked => {
        if (marked) {
          this.badge.refresh();
          // Re-read rather than patch: with the unread filter on, the rows just
          // marked no longer belong to the list at all.
          this.load();
        }
        then?.();
      },
      error: err => {
        this.handleError(err);
        // Still navigate: failing to record that a row was read is no reason to
        // keep the user off the booking it points at.
        then?.();
      }
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
