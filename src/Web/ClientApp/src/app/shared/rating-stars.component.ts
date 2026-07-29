import { Component, EventEmitter, Input, Output } from '@angular/core';

// The one way a rating is drawn anywhere in the app: five stars, optionally the
// number, optionally clickable.
//
// Display mode renders a full row of outline stars with a filled row clipped on
// top, so 4.3 looks like 4.3 rather than being rounded to a whole star. Input
// mode swaps in five buttons — the same shape, so a rated and an unrated rental
// read as the same control.
@Component({
  selector: 'app-rating-stars',
  templateUrl: './rating-stars.component.html',
  styleUrls: ['./rating-stars.component.css']
})
export class RatingStarsComponent {
  // Null = never rated. Deliberately distinct from 0, which would draw as the
  // worst possible score.
  @Input() value: number | null | undefined = null;
  // Reviews behind the score. 0 hides the count rather than showing "(0)".
  @Input() count: number | null | undefined = null;
  @Input() editable = false;
  @Input() size: 'sm' | 'md' | 'lg' = 'sm';
  // The numeric score next to the stars ("4.3"). Off where the stars alone say
  // enough, e.g. inside a single review.
  @Input() showValue = true;
  @Output() valueChange = new EventEmitter<number>();

  readonly stars = [1, 2, 3, 4, 5];

  // Set while the pointer is over the row, so the preview follows the cursor and
  // snaps back on leave instead of sticking at whatever was last hovered.
  hovered: number | null = null;

  get score(): number {
    return this.value ?? 0;
  }

  // Width of the filled overlay. Clamped: a score out of range is a bug, but it
  // must not paint outside the row.
  get fillPercent(): number {
    return Math.max(0, Math.min(100, (this.score / this.stars.length) * 100));
  }

  // What the buttons show right now — the hover preview if there is one, else
  // the committed value.
  get shown(): number {
    return this.hovered ?? this.score;
  }

  pick(star: number) {
    if (!this.editable) return;
    this.value = star;
    this.valueChange.emit(star);
  }
}
