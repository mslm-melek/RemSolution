import { Component, OnInit, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { CreateMyReviewCommand, MarketplaceClient, MyRentingDto, RentingState } from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';

// "My trips": the customer's rentals across every agency, and the only place a
// rating starts from — you rate a rental you actually took, not an agency you
// browsed.
@Component({
  selector: 'app-my-rentings',
  templateUrl: './my-rentings.component.html',
  styleUrls: ['./my-rentings.component.css']
})
export class MyRentingsComponent implements OnInit {
  // Error banners are plain strings, so they are translated imperatively rather
  // than through the template pipe.
  private readonly transloco = inject(TranslocoService);

  rentings: MyRentingDto[] = [];
  loading = true;
  error = '';

  // The rental whose rating form is open, or null. Only one at a time: the form
  // is an inline panel, and two open at once reads as two half-finished
  // reviews.
  ratingFor: MyRentingDto | null = null;
  rating = 0;
  comment = '';
  saving = false;
  formError = '';

  RentingState = RentingState;
  private labelKeys: { [key: number]: string } = {
    [RentingState.NotYet]: 'enums.rentingState.notYet',
    [RentingState.InProgress]: 'enums.rentingState.inProgress',
    [RentingState.Done]: 'enums.rentingState.done',
    [RentingState.Cancelled]: 'enums.rentingState.cancelled'
  };

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.client.getMyRentings().subscribe({
      next: list => { this.rentings = list || []; this.loading = false; },
      error: () => { this.error = this.transloco.translate('marketplace.loadFailed'); this.loading = false; }
    });
  }

  // Returns a transloco key; the template pipes it.
  stateLabelKey(state?: RentingState): string {
    return state === undefined || state === null ? '' : this.labelKeys[state] ?? '';
  }

  stateClass(state?: RentingState): string {
    switch (state) {
      case RentingState.Done: return 'done';
      case RentingState.InProgress: return 'active';
      case RentingState.NotYet: return 'upcoming';
      case RentingState.Cancelled: return 'cancelled';
      default: return '';
    }
  }

  openRating(renting: MyRentingDto) {
    this.ratingFor = renting;
    this.rating = 0;
    this.comment = '';
    this.formError = '';
  }

  cancelRating() {
    this.ratingFor = null;
    this.formError = '';
  }

  submitRating() {
    if (!this.ratingFor?.rentingId) return;

    // Stars are the review; the comment is optional. Guarded here as well as on
    // the server so a mis-click does not cost a round trip.
    if (!this.rating) {
      this.formError = this.transloco.translate('reviews.pickAStar');
      return;
    }

    this.saving = true;
    this.formError = '';

    const rentingId = this.ratingFor.rentingId;
    const command = new CreateMyReviewCommand();
    command.rentingId = rentingId;
    command.rating = this.rating;
    command.comment = this.comment?.trim() || undefined;

    this.client.reviewMyRenting(rentingId, command).subscribe({
      next: () => {
        this.saving = false;
        this.ratingFor = null;
        // Reloaded rather than patched in place: the server decides whether a
        // rental can still be rated, and it has just changed its mind.
        this.load();
      },
      error: err => {
        this.saving = false;
        this.formError = extractValidationErrors(err)
          ?? this.transloco.translate('reviews.saveFailed');
      }
    });
  }
}
