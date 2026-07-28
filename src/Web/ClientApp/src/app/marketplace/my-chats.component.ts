import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import {
  MarketplaceClient, MyChatThreadDto, ChatMessageDto, ChatAuthorKind,
  SendCustomerChatMessageCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

// Same cadence and cursor contract as the agency inbox (see ChatComponent).
const POLL_INTERVAL_MS = 10_000;

@Component({
  selector: 'app-my-chats',
  templateUrl: './my-chats.component.html',
  // Deliberately shares the agency chat stylesheet: it is the same conversation
  // UI seen from the other side, and one copy keeps the two from drifting.
  styleUrls: ['../chat/chat.component.css']
})
export class MyChatsComponent implements OnInit, OnDestroy {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  threads: MyChatThreadDto[] = [];
  selected?: MyChatThreadDto;
  messages: ChatMessageDto[] = [];
  draft = '';
  sending = false;
  errorMessage = '';

  ChatAuthorKind = ChatAuthorKind;

  private poll?: Subscription;

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    this.loadThreads();
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  loadThreads() {
    this.client.getMyChatThreads().subscribe({
      next: threads => {
        this.threads = threads || [];

        if (this.selected) {
          const refreshed = this.threads.find(x => x.rentingId === this.selected!.rentingId);
          if (refreshed) this.selected = refreshed;
        }
      },
      error: err => this.handleError(err)
    });
  }

  open(thread: MyChatThreadDto) {
    this.selected = thread;
    this.messages = [];
    this.draft = '';
    this.errorMessage = '';

    if (!thread.rentingId) return;

    this.client.getMyChatMessages(thread.rentingId, null).subscribe({
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

  carLabel(thread: MyChatThreadDto): string {
    return [thread.carBrandName, thread.carModelName].filter(x => !!x).join(' ')
      || thread.carMatricule
      || '';
  }

  private startPolling(rentingId: number) {
    this.stopPolling();
    this.poll = timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS).pipe(
      switchMap(() => this.client.getMyChatMessages(rentingId, this.lastMessageId()))
    ).subscribe({
      next: incoming => {
        if (!this.appendMessages(incoming)) return;
        this.markRead();
        this.loadThreads();
      },
      error: err => console.error(err)
    });
  }

  // Same guard as the agency side: a poll tick and the post-send re-read can
  // both carry the same cursor and return the same message.
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

  // The customer's unread are the AGENCY's messages (the mirror of the desk side).
  private markRead() {
    if (!this.selected?.rentingId) return;
    if (!this.messages.some(m => m.authorKind === ChatAuthorKind.Agency && !m.readAt)) return;

    this.client.markMyChatRead(this.selected.rentingId).subscribe({
      next: () => this.loadThreads(),
      error: err => console.error(err)
    });
  }

  // Plain Enter sends; the default newline is suppressed (see ChatComponent).
  send(event?: Event) {
    event?.preventDefault();

    const body = this.draft.trim();
    if (!body || !this.selected?.rentingId) return;

    this.sending = true;
    this.errorMessage = '';
    const rentingId = this.selected.rentingId;
    const command = new SendCustomerChatMessageCommand({ rentingId, body });

    this.client.sendMyChatMessage(rentingId, command).subscribe({
      next: () => {
        this.sending = false;
        this.draft = '';
        this.client.getMyChatMessages(rentingId, this.lastMessageId()).subscribe({
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
