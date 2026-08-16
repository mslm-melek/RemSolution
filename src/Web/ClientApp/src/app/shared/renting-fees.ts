import { RentingFeeKind, RentingFeeDto } from '../web-api-client';

// The charges a return can establish, in the order a counter reads them out:
// what everyone charges for first, "anything else" last. Two screens draw this
// list — the return dialog and the booking's money tab — so the order and the
// labels are stated once.
export const RENTING_FEE_KINDS: RentingFeeKind[] = [
  RentingFeeKind.Late,
  RentingFeeKind.Damage,
  RentingFeeKind.ExcessMileage,
  RentingFeeKind.Fuel,
  RentingFeeKind.Cleaning,
  RentingFeeKind.Other,
];

// The enum is numeric over the wire, so the key cannot be built from the value.
const LABELS: Record<number, string> = {
  [RentingFeeKind.Late]: 'renting.feeKind.late',
  [RentingFeeKind.Damage]: 'renting.feeKind.damage',
  [RentingFeeKind.ExcessMileage]: 'renting.feeKind.excessMileage',
  [RentingFeeKind.Fuel]: 'renting.feeKind.fuel',
  [RentingFeeKind.Cleaning]: 'renting.feeKind.cleaning',
  [RentingFeeKind.Other]: 'renting.feeKind.other',
};

export function feeKindLabelKey(kind?: RentingFeeKind): string {
  return LABELS[kind ?? RentingFeeKind.Other] ?? LABELS[RentingFeeKind.Other];
}

export function feesTotal(fees: RentingFeeDto[]): number {
  return fees.reduce((sum, fee) => sum + (fee.amount?.amount ?? 0), 0);
}
