import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import {
  ChatClient, ChatThreadDto, ChatMessageDto, ChatAuthorKind,
  SendChatMessageCommand, RentingState
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { applyListFilters, boolParam } from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';

// Poll interval while a thread is open. Deliberately not a socket: the API
// exposes an id-cursor read (?afterId=), which any hosting can serve.
const POLL_INTERVAL_MS = 10_000;

@Component({
  selector: 'app-chat',
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.css']
})
export class ChatComponent implements OnInit, OnDestroy {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  threads: ChatThreadDto[] = [];
  selected?: ChatThreadDto;
  messages: ChatMessageDto[] = [];
  draft = '';
  sending = false;
  onlyUnread = false;
  errorMessage = '';

  canSend = false;

  ChatAuthorKind = ChatAuthorKind;
  RentingState = RentingState;

  private poll?: Subscription;

  constructor(
    private client: ChatClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canSend = AuthService.canAccessModule(user, 'Chat', 'Chat.Send');
    });

    // The unread filter lives in the URL (see shared/list-filters): the home tile
    // counts threads waiting for a reply, so its link opens those.
    this.route.queryParamMap.subscribe(params => {
      this.onlyUnread = boolParam(params, 'unread') === true;
      this.loadThreads();
    });
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  loadThreads() {
    this.client.getThreads(1, 50, this.onlyUnread).subscribe({
      next: result => {
        this.threads = result.items || [];

        // Keep the open thread's row in step with the refreshed list.
        if (this.selected) {
          const refreshed = this.threads.find(x => x.rentingId === this.selected!.rentingId);
          if (refreshed) this.selected = refreshed;
        }
      },
      error: err => this.handleError(err)
    });
  }

  // The filter goes through the URL; the subscription above reloads the threads.
  onUnreadFilter() {
    applyListFilters(this.router, this.route, { unread: this.onlyUnread ? 'true' : null });
  }

  open(thread: ChatThreadDto) {
    this.selected = thread;
    this.messages = [];
    this.draft = '';
    this.errorMessage = '';

    if (!thread.rentingId) return;

    this.client.getMessages(thread.rentingId, null).subscribe({
      next: messages => {
        this.messages = messages || [];
        this.markRead();
      },
      error: err => this.handleError(err)
    });

    this.startPolling(thread.rentingId);
  }

  close() {
    this.stopPolling();
    this.selected = undefined;
    this.messages = [];
  }

  // Only fetches what arrived after the newest message already held, so an open
  // thread costs one small request per tick.
  private startPolling(rentingId: number) {
    this.stopPolling();
    this.poll = timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS).pipe(
      switchMap(() => this.client.getMessages(rentingId, this.lastMessageId()))
    ).subscribe({
      next: incoming => {
        if (!this.appendMessages(incoming)) return;
        this.markRead();
        // A new message changes the list's ordering and unread badges.
        this.loadThreads();
      },
      error: err => console.error(err)
    });
  }

  // A poll tick and the re-read that follows a send can be in flight with the
  // same cursor, so both can return the same message. Appending by id keeps the
  // thread from showing it twice. Returns whether anything was actually added.
  private appendMessages(incoming: ChatMessageDto[] | null): boolean {
    const known = new Set(this.messages.map(m => m.id));
    const fresh = (incoming || []).filter(m => !known.has(m.id));

    if (!fresh.length) return false;

    this.messages = [...this.messages, ...fresh];
    return true;
  }

  private stopPolling() {
    this.poll?.unsubscribe();
    this.poll = undefined;
  }

  private lastMessageId(): number | null {
    return this.messages.length ? (this.messages[this.messages.length - 1].id ?? null) : null;
  }

  private markRead() {
    if (!this.selected?.rentingId) return;
    if (!this.messages.some(m => m.authorKind === ChatAuthorKind.Client && !m.readAt)) return;

    this.client.markRead(this.selected.rentingId).subscribe({
      next: () => this.loadThreads(),
      error: err => console.error(err)
    });
  }

  // Plain Enter sends (Shift+Enter still inserts a newline, which Angular routes
  // to a different pseudo-event); the default would also type that newline into
  // the box, so it is suppressed.
  send(event?: Event) {
    event?.preventDefault();

    const body = this.draft.trim();
    if (!body || !this.selected?.rentingId) return;

    this.sending = true;
    this.errorMessage = '';
    const rentingId = this.selected.rentingId;
    const command = new SendChatMessageCommand({ rentingId, body });

    this.client.sendMessage(rentingId, command).subscribe({
      next: () => {
        this.sending = false;
        this.draft = '';
        // Re-read from the cursor so the stored message (with its server
        // timestamp and id) is what lands in the thread, not a local echo.
        this.client.getMessages(rentingId, this.lastMessageId()).subscribe({
          next: incoming => {
            this.appendMessages(incoming);
            this.loadThreads();
          },
          error: err => console.error(err)
        });
      },
      error: err => {
        this.sending = false;
        this.handleError(err);
      }
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
