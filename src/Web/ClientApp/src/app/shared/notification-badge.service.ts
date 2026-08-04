import { Injectable, OnDestroy, inject } from '@angular/core';
import { BehaviorSubject, Observable, Subscription, of, timer } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { NotificationsClient } from '../web-api-client';
import { AuthService } from './auth.service';

// How often the bell re-asks. Slower than the chat poll (10s): an unread count is
// ambient information, and this request rides along on every screen in the app.
const POLL_INTERVAL_MS = 60_000;

/**
 * The unread count behind the bell in the navigation bar.
 *
 * A service rather than component state because two screens read the same figure:
 * the bell shows it from wherever the user is, and the notification centre clears
 * it as they read. Polled on a timer for the same reason chat is — the API is a
 * plain count any hosting can serve, with no socket transport involved.
 */
@Injectable({ providedIn: 'root' })
export class NotificationBadgeService implements OnDestroy {
  private readonly client = inject(NotificationsClient);
  private readonly auth = inject(AuthService);

  private readonly count = new BehaviorSubject<number>(0);
  private poll?: Subscription;

  /** Unread notifications addressed to the signed-in user. */
  readonly unreadCount$: Observable<number> = this.count.asObservable();

  constructor() {
    // Only where the agency actually has the module: without the feature every
    // request would come back 403, once a minute, forever.
    this.auth.currentUser$.subscribe(user => {
      if (user.features?.includes('Notifications')) {
        this.start();
      } else {
        this.stop();
      }
    });
  }

  ngOnDestroy() {
    this.stop();
  }

  /**
   * Re-reads the count now. Called after the centre marks rows read, so the badge
   * does not sit on a stale figure until the next tick.
   */
  refresh() {
    this.client.getUnreadCount().subscribe({
      next: count => this.count.next(count ?? 0),
      error: err => console.error(err)
    });
  }

  /** The figure the bell is currently showing. */
  get current(): number {
    return this.count.value;
  }

  private start() {
    if (this.poll) return;

    // Fires immediately, then on the interval. The failure is caught INSIDE the
    // switchMap on purpose: an error reaching the outer stream would complete the
    // subscription, and one blip — a restart mid-request — would silently kill
    // the badge for the rest of the session. Logged rather than surfaced, since a
    // background count must not put an error on whatever screen the user is
    // actually working on.
    this.poll = timer(0, POLL_INTERVAL_MS).pipe(
      switchMap(() => this.client.getUnreadCount().pipe(
        catchError(err => {
          console.error(err);
          return of(this.count.value);
        })
      ))
    ).subscribe(count => this.count.next(count ?? 0));
  }

  private stop() {
    this.poll?.unsubscribe();
    this.poll = undefined;
    this.count.next(0);
  }
}
