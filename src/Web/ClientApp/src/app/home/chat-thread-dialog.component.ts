import { AfterViewChecked, Component, ElementRef, Inject, OnInit, ViewChild, inject } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { extractValidationErrors } from '../shared/form-utils';
import {
  ChatAuthorKind, ChatClient, ChatMessageDto, ChatThreadDto, SendChatMessageCommand
} from '../web-api-client';

export interface ChatThreadDialogData {
  thread: ChatThreadDto;
  /** Whether this user may write, as opposed to only read the history. */
  canSend: boolean;
}

/**
 * One conversation, opened from the landing screen's strip.
 *
 * Deliberately the whole thread and not a preview: answering "can I add a child
 * seat?" needs the question in front of you, and a dialog that only showed the
 * last line would send everybody to the chat screen anyway. No polling — this is
 * opened to deal with something and closed again, and the strip re-reads itself
 * when it closes; the chat screen is where a conversation is kept open.
 */
@Component({
  selector: 'app-chat-thread-dialog',
  templateUrl: './chat-thread-dialog.component.html',
  styleUrls: ['./chat-thread-dialog.component.css']
})
export class ChatThreadDialogComponent implements OnInit, AfterViewChecked {
  private readonly chat = inject(ChatClient);
  private readonly transloco = inject(TranslocoService);

  @ViewChild('scroller') threadEl?: ElementRef<HTMLElement>;

  messages: ChatMessageDto[] = [];
  draft = '';
  loading = true;
  sending = false;
  errorMessage = '';

  readonly ChatAuthorKind = ChatAuthorKind;

  // Scrolls to the newest message once, after each load or send, rather than on
  // every change-detection pass — otherwise the reader could never scroll up.
  private scrollPending = true;

  constructor(@Inject(MAT_DIALOG_DATA) public data: ChatThreadDialogData) { }

  get thread(): ChatThreadDto {
    return this.data.thread;
  }

  /**
   * What the conversation is about — the dialog's subtitle. The client is the
   * heading, so this is the booking: which car, and when it runs.
   */
  get context(): string {
    const period = [this.thread.startDate, this.thread.endDate]
      .filter((date): date is Date => !!date)
      .map(date => formatUtcDay(date))
      .join(' → ');

    return [this.thread.carMatricule, period].filter(Boolean).join(' · ');
  }

  get initials(): string {
    const parts = (this.thread.clientName ?? '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  /** Writable: the user may send AND the hire is still open (see ChatThreadDto). */
  get canWrite(): boolean {
    return this.data.canSend && this.thread.isOpen === true;
  }

  ngOnInit() {
    this.load();
  }

  ngAfterViewChecked() {
    if (!this.scrollPending || !this.threadEl) return;

    this.scrollPending = false;
    this.threadEl.nativeElement.scrollTop = this.threadEl.nativeElement.scrollHeight;
  }

  send() {
    const body = this.draft.trim();
    if (!body || this.sending) return;

    this.sending = true;
    this.errorMessage = '';

    this.chat.sendMessage(this.thread.rentingId!, new SendChatMessageCommand({
      rentingId: this.thread.rentingId,
      body
    })).subscribe({
      next: () => {
        this.sending = false;
        this.draft = '';
        this.load();
      },
      error: err => {
        this.sending = false;
        this.errorMessage =
          extractValidationErrors(err) ?? this.transloco.translate('common.actionFailed');
      }
    });
  }

  private load() {
    this.chat.getMessages(this.thread.rentingId!, null).subscribe({
      next: messages => {
        this.loading = false;
        this.messages = messages ?? [];
        this.scrollPending = true;
        this.markRead();
      },
      error: err => {
        this.loading = false;
        this.errorMessage = this.transloco.translate('common.actionFailed');
        console.error(err);
      }
    });
  }

  // Opening the thread IS reading it, exactly as the chat screen treats it. The
  // strip re-reads its counts when the dialog closes, so a failure here corrects
  // itself on the next open rather than needing to be reported.
  private markRead() {
    if (!this.thread.unreadCount) return;

    this.chat.markRead(this.thread.rentingId!).subscribe({
      next: () => { },
      error: err => console.error(err)
    });
  }
}

/**
 * `dd/MM/yyyy` from the UTC parts. The API's dates are wall-clock values stamped
 * UTC (see form-utils), so the local calendar would name the day before in a
 * negative offset.
 */
function formatUtcDay(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${pad(date.getUTCDate())}/${pad(date.getUTCMonth() + 1)}/${date.getUTCFullYear()}`;
}
