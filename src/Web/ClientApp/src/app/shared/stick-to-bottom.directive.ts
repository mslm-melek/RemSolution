import { AfterViewChecked, Directive, ElementRef, inject } from '@angular/core';

// How far from the bottom still counts as "reading the newest". Roughly one
// message's worth, so a stack that grows by one does not have to land pixel-
// perfect to keep following.
const NEAR_BOTTOM_PX = 60;

/**
 * Keeps a scrolling pane pinned to its newest content — a message thread.
 *
 * Only when the content actually grew, and only when the reader was already at
 * the bottom: somebody who has scrolled up to re-read something must not be
 * yanked back down, either by an arriving message or by a change-detection pass
 * that changed nothing.
 */
@Directive({
  selector: '[appStickToBottom]'
})
export class StickToBottomDirective implements AfterViewChecked {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private lastHeight = -1;

  ngAfterViewChecked() {
    const el = this.host.nativeElement;
    if (el.scrollHeight === this.lastHeight) return;

    // First paint counts as "at the bottom": a thread opens on its newest
    // message, which is the one the reader came for.
    const wasAtBottom = this.lastHeight < 0
      || el.scrollTop + el.clientHeight >= this.lastHeight - NEAR_BOTTOM_PX;

    this.lastHeight = el.scrollHeight;
    if (wasAtBottom) el.scrollTop = el.scrollHeight;
  }
}
