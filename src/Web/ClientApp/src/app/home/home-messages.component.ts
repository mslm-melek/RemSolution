import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from '../shared/auth.service';
import { ChatThreadDialogComponent } from './chat-thread-dialog.component';
import { ChatClient, ChatThreadDto } from '../web-api-client';

// The strip is a glance, not the inbox: the conversations most recently spoken
// on. Everything else is one click away on the chat screen.
const STRIP_SIZE = 12;

/**
 * The conversation strip at the foot of the landing screen.
 *
 * A row of faces rather than a list of rows: what the desk needs from the home
 * page is "is anyone waiting on me", which is a count and a name, and answering
 * it costs one line at the bottom of the screen instead of a panel competing
 * with the day's work. Opening one lands in a dialog that can be replied to
 * without leaving the page — the whole point of having it here.
 */
@Component({
  selector: 'app-home-messages',
  templateUrl: './home-messages.component.html',
  styleUrls: ['./home-messages.component.css']
})
export class HomeMessagesComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly chat = inject(ChatClient);
  private readonly dialog = inject(MatDialog);

  visible = false;
  canSend = false;
  loading = false;

  threads: ChatThreadDto[] = [];
  total = 0;

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.visible = AuthService.canAccessModule(user, 'Chat', 'Chat.View');
      this.canSend = AuthService.canAccessModule(user, 'Chat', 'Chat.Send');

      if (this.visible) this.load();
    });
  }

  /** Messages from clients nobody has read yet, across every thread on screen. */
  get unread(): number {
    return this.threads.reduce((sum, thread) => sum + (thread.unreadCount ?? 0), 0);
  }

  get waiting(): number {
    return this.threads.filter(thread => (thread.unreadCount ?? 0) > 0).length;
  }

  /** Two letters from the client's name, however it is spelled. */
  initials(thread: ChatThreadDto): string {
    const parts = (thread.clientName ?? '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  /** What the circle's tooltip says: who, and about which car. */
  label(thread: ChatThreadDto): string {
    return [thread.clientName, thread.carMatricule].filter(Boolean).join(' · ');
  }

  open(thread: ChatThreadDto) {
    this.dialog.open(ChatThreadDialogComponent, {
      data: { thread, canSend: this.canSend },
      width: '460px',
      maxWidth: '95vw',
      autoFocus: 'first-tabbable'
    })
      // Opening a thread marks it read and may have added a reply, so the strip
      // is re-read rather than guessed at.
      .afterClosed().subscribe(() => this.load());
  }

  private load() {
    this.loading = true;

    this.chat.getThreads(1, STRIP_SIZE, false).subscribe({
      next: result => {
        this.loading = false;
        this.threads = result.items ?? [];
        this.total = result.totalCount ?? 0;
      },
      // A strip that cannot be filled is left off the screen rather than
      // explained: the chat screen reports its own failures.
      error: err => {
        this.loading = false;
        this.threads = [];
        console.error(err);
      }
    });
  }
}
