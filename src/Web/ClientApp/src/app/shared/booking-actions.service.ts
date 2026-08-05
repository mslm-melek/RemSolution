import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TranslocoService } from '@jsverse/transloco';
import {
  ChangeRentingStateCommand, ConvertReservationCommand, RejectReservationCommand,
  RentingDto, RentingState, ReservationDto, ReservationsClient, RentingsClient
} from '../web-api-client';
import { extractProblemDetail, extractValidationErrors, isInvalidTransition } from './form-utils';
import { ReturnDialogComponent } from './return-dialog.component';

/**
 * The booking actions that are offered from more than one screen: approving or
 * declining a hold, turning it into a hire, handing a car over, and taking one
 * back. The bookings lists offer them on their rows, and so does the home
 * screen's work queue — same prompts, same wording, same reading of a failure,
 * because they are the same actions.
 *
 * Every method answers with an {@link BookingActionOutcome} instead of throwing:
 * a refusal here is usually not a bug but news ("someone else already confirmed
 * this"), and the caller's job is to show it and reload. `changed` is the only
 * thing a caller has to act on — it is true whenever the rows on screen are no
 * longer what the server holds, including after a stale-state failure.
 */
export interface BookingActionOutcome {
  /** The list should reload: something moved, or what is on screen is stale. */
  changed: boolean;
  /** A message to show the user, already translated. Empty when there is none. */
  error: string;
  /** Conversion only: the hire the hold became, for the caller to open. */
  rentingId?: number;
}

/** Nothing happened — the user backed out of a prompt. */
const NOTHING: BookingActionOutcome = { changed: false, error: '' };

@Injectable({ providedIn: 'root' })
export class BookingActionsService {
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  private readonly reservations = inject(ReservationsClient);
  private readonly rentings = inject(RentingsClient);

  // --- Reservations ---------------------------------------------------------

  /** Approves the hold. Approving does NOT create the hire — converting does. */
  confirmReservation(reservation: ReservationDto): Observable<BookingActionOutcome> {
    if (!reservation.id) return of(NOTHING);

    return this.reservations.confirmReservation(reservation.id).pipe(
      map(() => ({ changed: true, error: '' })),
      catchError(err => of(this.failed(err)))
    );
  }

  /** Declines the hold with the reason the client is shown. Asks for it first. */
  rejectReservation(reservation: ReservationDto): Observable<BookingActionOutcome> {
    if (!reservation.id) return of(NOTHING);

    const reason = prompt(this.transloco.translate('reservation.promptRejectReason'));
    if (!reason) return of(NOTHING);

    const command = new RejectReservationCommand({ id: reservation.id, reason });

    return this.reservations.rejectReservation(reservation.id, command).pipe(
      map(() => ({ changed: true, error: '' })),
      catchError(err => of(this.failed(err)))
    );
  }

  /**
   * Turns the hold into a hire. The driver's identity document is asked for
   * because conversion dedupes the client on it (see ConvertReservationCommand);
   * the outcome carries the new hire's id so the caller can open it.
   */
  convertReservation(reservation: ReservationDto): Observable<BookingActionOutcome> {
    if (!reservation.id) return of(NOTHING);
    if (!confirm(this.transloco.translate('reservation.confirmConvert'))) return of(NOTHING);

    const cin = prompt(this.transloco.translate('reservation.promptDriverCin')) || undefined;
    const passeportNumber = cin
      ? undefined
      : (prompt(this.transloco.translate('reservation.promptDriverPassport')) || undefined);

    const command = new ConvertReservationCommand({ id: reservation.id, cin, passeportNumber });

    return this.reservations.convertReservation(reservation.id, command).pipe(
      map(rentingId => ({ changed: true, error: '', rentingId })),
      catchError(err => of(this.failed(err)))
    );
  }

  // --- Rentings -------------------------------------------------------------

  /**
   * Hands the car over: the customer is at the counter, so the hire goes out
   * from wherever it is listed. The odometer offered is what the booking
   * recorded, or the car's own reading when it was booked without one; the agent
   * overtypes it with the dashboard, and that is what moves the car's figure on
   * (see Car.RecordOdometer).
   */
  startRenting(renting: RentingDto): Observable<BookingActionOutcome> {
    if (!renting.id) return of(NOTHING);

    const offered = renting.startMileage ?? renting.carMileage;

    const value = prompt(
      this.transloco.translate('renting.promptPickupMileage'),
      offered === null || offered === undefined ? '' : String(offered));

    if (value === null) return of(NOTHING); // cancelled

    const command = new ChangeRentingStateCommand({
      id: renting.id,
      rowVersion: renting.rowVersion,
      newState: RentingState.InProgress,
      mileage: value.trim() === '' ? undefined : Number(value)
    });

    return this.rentings.changeRentingState(renting.id, command).pipe(
      map(() => ({ changed: true, error: '' })),
      catchError(err => of(this.failed(err)))
    );
  }

  /**
   * Brings the car back in through the dialog that prices an early or late
   * return. The dialog re-reads the hire itself, so a row that has been sitting
   * on screen is enough to open it.
   */
  returnRenting(renting: RentingDto): Observable<BookingActionOutcome> {
    if (!renting.id) return of(NOTHING);

    return this.dialog.open(ReturnDialogComponent, {
      data: {
        rentingId: renting.id,
        carLabel: [renting.carMatricule, renting.carModelName].filter(Boolean).join(' · '),
        clientName: renting.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().pipe(
      map(returned => ({ changed: returned === true, error: '' }))
    );
  }

  // --- Failures -------------------------------------------------------------

  private failed(err: any): BookingActionOutcome {
    // The booking moved on while the list was on screen (someone else confirmed
    // it, or brought the car back). The row is stale, so say what happened and
    // have the caller reload rather than leaving the old state and its now-wrong
    // action buttons up.
    if (isInvalidTransition(err)) {
      return { changed: true, error: this.transloco.translate('reservation.staleState') };
    }

    // What the server said, whether it came as a validation map or as a plain
    // problem detail (a booking conflict, an inactive subscription, a plan limit):
    // its own words say more than "the action could not be completed".
    const said = extractValidationErrors(err) ?? extractProblemDetail(err);
    if (!said) console.error(err);

    return {
      changed: false,
      error: said ?? this.transloco.translate('common.actionFailed')
    };
  }
}
