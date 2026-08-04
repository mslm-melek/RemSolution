import { Component, OnInit, inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { Observable, of, switchMap, tap } from 'rxjs';
import {
  RentingsClient, RentingDto, RentingState,
  ChangeRentingStateCommand, ChangeRentingEndDateCommand
} from '../web-api-client';
import { extractValidationErrors, fromDateInput, toDateInput } from './form-utils';

export interface ReturnDialogData {
  rentingId: number;
  // Shown under the title when the caller already knows them — a car row does,
  // and re-reading them would only make the dialog slower to appear.
  carLabel?: string;
  clientName?: string;
}

// Brings a car back in from wherever the hire is listed — the cars list, a car's
// page, the booking itself — instead of making the user find the booking and
// answer a browser prompt.
//
// The renting is re-read on open rather than passed in: the concurrency token has
// to be a fresh one (a list row's ages as the page sits open), and the pickup
// odometer is the floor the return reading has to clear.
@Component({
  selector: 'app-return-dialog',
  templateUrl: './return-dialog.component.html',
  styleUrls: ['./return-dialog.component.css']
})
export class ReturnDialogComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<ReturnDialogData>(MAT_DIALOG_DATA);

  renting?: RentingDto;
  form: FormGroup;
  loading = true;
  saving = false;
  errorMessage = '';

  // The end date as the booking has it; the field starts there, so leaving it
  // alone means "returned as scheduled" and touches no price.
  private scheduledEnd = '';

  // The return is two writes when the date moved, and the first one is not undone
  // if the second fails (see submit). Remembering it landed is what lets a retry
  // pick up from the right place, and what tells the caller its figures are stale
  // even if the user then gives up.
  private endDateMoved = false;

  constructor(
    private fb: FormBuilder,
    private rentings: RentingsClient,
    private dialog: MatDialogRef<ReturnDialogComponent, boolean>
  ) {
    this.form = this.fb.group({
      // Optional, like the state endpoint itself: an agency that does not track
      // the odometer must still be able to close the hire.
      mileage: [null, this.notBelowPickup.bind(this)],
      returnDate: ['']
    });
  }

  ngOnInit() {
    this.rentings.getRentingById(this.data.rentingId).subscribe({
      next: renting => {
        this.applyRenting(renting);
        // Only on the first read: a re-read after a failure must not overwrite the
        // day the user typed (see reload).
        this.form.patchValue({ returnDate: this.scheduledEnd });
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.handleError(err);
      }
    });
  }

  private applyRenting(renting: RentingDto) {
    this.renting = renting;
    this.scheduledEnd = toDateInput(renting.endDate);
  }

  // Re-reads the booking after a failed submit. The date field is left as typed,
  // so `endDateChanged` now answers truthfully for the retry: false once the move
  // landed (the booking says that day too), still true if it was the move itself
  // that failed.
  private reload() {
    this.rentings.getRentingById(this.data.rentingId).subscribe({
      next: renting => this.applyRenting(renting),
      // The banner already says what went wrong; this is only about the token.
      error: err => console.error(err)
    });
  }

  get carLabel(): string {
    return this.data.carLabel
      ?? [this.renting?.carMatricule, this.renting?.carModelName].filter(Boolean).join(' · ');
  }

  get clientName(): string {
    return this.data.clientName ?? this.renting?.clientName ?? '';
  }

  get pickupMileage(): number | undefined {
    return this.renting?.startMileage ?? undefined;
  }

  /**
   * The hire is being closed on a different day than booked, so the price moves.
   * A booking with no end date at all counts as changed the moment a day is
   * picked — that is the day being set.
   */
  get endDateChanged(): boolean {
    const chosen = this.form.value.returnDate;
    return !!chosen && chosen !== this.scheduledEnd;
  }

  submit() {
    if (this.form.invalid || !this.renting || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    // Returned early or late: move the end date first so the hire is re-priced
    // for the days actually taken (see ChangeRentingEndDateCommand) — the state
    // change itself never touches the price. Done is refused by that endpoint, so
    // the order matters.
    this.moveEndDateIfNeeded()
      .pipe(switchMap(renting => this.complete(renting)))
      .subscribe({
        next: () => this.dialog.close(true),
        error: err => {
          this.saving = false;
          this.handleError(err);
          // Whatever failed, the booking may have moved under us — the date write
          // may well be the one that landed. Re-read it so the retry carries a
          // valid concurrency token and does not redo a step that is already done.
          this.reload();
        }
      });
  }

  cancel() {
    // Giving up after the end date already moved still leaves the caller's
    // figures stale, so closing asks for a reload (same reasoning as the payment
    // dialog's half-done state).
    this.dialog.close(this.endDateMoved);
  }

  // Returns the renting the return should be recorded against: the reloaded one
  // when the end date moved (its concurrency token has changed), otherwise the
  // one already in hand.
  private moveEndDateIfNeeded(): Observable<RentingDto> {
    const renting = this.renting!;

    if (!this.endDateChanged) return of(renting);

    const endDate = fromDateInput(this.form.value.returnDate);
    if (!endDate) return of(renting);

    const command = new ChangeRentingEndDateCommand({
      id: renting.id,
      rowVersion: renting.rowVersion,
      endDate,
      // The paperwork is a decision of its own (it needs a template and possibly
      // manual values), so it stays on the renting screen.
      regenerateContract: false
    });

    return this.rentings.changeRentingEndDate(renting.id!, command)
      .pipe(
        tap(() => {
          this.endDateMoved = true;
          // A write has landed. Escape and backdrop clicks resolve the dialog
          // without going through cancel(), which would tell the caller nothing
          // happened and leave a stale row on screen — so from here the dialog is
          // only left through its own buttons.
          this.dialog.disableClose = true;
        }),
        switchMap(() => this.rentings.getRentingById(renting.id!)),
        tap(fresh => this.applyRenting(fresh))
      );
  }

  private complete(renting: RentingDto): Observable<void> {
    const mileage = this.form.value.mileage;

    return this.rentings.changeRentingState(renting.id!, new ChangeRentingStateCommand({
      id: renting.id,
      rowVersion: renting.rowVersion,
      newState: RentingState.Done,
      mileage: mileage === null || mileage === '' ? undefined : Number(mileage)
    }));
  }

  // The server refuses a reading below the pickup one; saying so here saves the
  // round-trip and points at the field.
  private notBelowPickup(control: AbstractControl): ValidationErrors | null {
    const pickup = this.renting?.startMileage;
    if (control.value === null || control.value === '' || pickup === undefined || pickup === null) {
      return null;
    }
    return Number(control.value) < pickup ? { belowPickup: true } : null;
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
