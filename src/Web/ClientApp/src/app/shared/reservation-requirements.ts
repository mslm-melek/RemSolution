import { ReservationRequirementKind, ReservationRequirementStatus } from '../web-api-client';

// What an agency can ask for before pickup, in the order a counter reads it out:
// the money first, then the paperwork. Two screens draw this list — the agency's
// booking panel and the customer's own checklist — so it is stated once.
export const REQUIREMENT_KINDS: ReservationRequirementKind[] = [
  ReservationRequirementKind.Payment,
  ReservationRequirementKind.Deposit,
  ReservationRequirementKind.Document,
  ReservationRequirementKind.Conditions,
  ReservationRequirementKind.Contract,
];

// Both enums are numeric over the wire, so the key cannot be built from the value.
const KIND_LABELS: Record<number, string> = {
  [ReservationRequirementKind.Payment]: 'reservation.requirements.kinds.payment',
  [ReservationRequirementKind.Deposit]: 'reservation.requirements.kinds.deposit',
  [ReservationRequirementKind.Document]: 'reservation.requirements.kinds.document',
  [ReservationRequirementKind.Conditions]: 'reservation.requirements.kinds.conditions',
  [ReservationRequirementKind.Contract]: 'reservation.requirements.kinds.contract',
};

const STATUS_LABELS: Record<number, string> = {
  [ReservationRequirementStatus.Requested]: 'reservation.requirements.states.requested',
  [ReservationRequirementStatus.Submitted]: 'reservation.requirements.states.submitted',
  [ReservationRequirementStatus.Accepted]: 'reservation.requirements.states.accepted',
  [ReservationRequirementStatus.Rejected]: 'reservation.requirements.states.rejected',
  [ReservationRequirementStatus.Waived]: 'reservation.requirements.states.waived',
};

export function requirementKindLabelKey(kind?: ReservationRequirementKind): string {
  return KIND_LABELS[kind ?? ReservationRequirementKind.Document]
    ?? KIND_LABELS[ReservationRequirementKind.Document];
}

export function requirementStatusLabelKey(status?: ReservationRequirementStatus): string {
  return STATUS_LABELS[status ?? ReservationRequirementStatus.Requested]
    ?? STATUS_LABELS[ReservationRequirementStatus.Requested];
}

/** Settled: the agency has what it asked for, or no longer wants it. */
export function isRequirementSettled(status?: ReservationRequirementStatus): boolean {
  return status === ReservationRequirementStatus.Accepted
    || status === ReservationRequirementStatus.Waived;
}
