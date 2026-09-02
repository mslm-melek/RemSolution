import { ReservationStatus } from '../web-api-client';

// What a reservation's status is CALLED, in one place.
//
// A customer meets the same seven statuses on the home page and in their own
// list of bookings, and two screens wording one of them differently is how
// somebody stops trusting both.
const labelKeys: Record<ReservationStatus, string> = {
  [ReservationStatus.PendingConfirmation]: 'enums.reservationStatus.pendingConfirmation',
  [ReservationStatus.Confirmed]: 'enums.reservationStatus.confirmed',
  [ReservationStatus.Cancelled]: 'enums.reservationStatus.cancelled',
  [ReservationStatus.Expired]: 'enums.reservationStatus.expired',
  [ReservationStatus.Rejected]: 'enums.reservationStatus.rejected',
  [ReservationStatus.Paid]: 'enums.reservationStatus.paid',
  [ReservationStatus.Converted]: 'enums.reservationStatus.converted'
};

export function reservationStatusLabelKey(status?: ReservationStatus): string {
  return status === undefined || status === null ? '' : labelKeys[status] ?? '';
}

// Tone class for the global `.chip` (see styles.scss). Confirmed, paid and
// converted all mean the hire is going ahead; a hold still waiting on the
// agency is work outstanding; a refusal is the only one that is bad news; and a
// hold the customer dropped or let lapse is neither, so it stays quiet.
export function reservationStatusTone(status?: ReservationStatus): string {
  switch (status) {
    case ReservationStatus.Confirmed:
    case ReservationStatus.Paid:
    case ReservationStatus.Converted:
      return 'ok';
    case ReservationStatus.PendingConfirmation:
      return 'warn';
    case ReservationStatus.Rejected:
      return 'danger';
    default:
      return 'neutral';
  }
}

