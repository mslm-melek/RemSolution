import { CarUnavailabilityReason } from '../web-api-client';

// Why a car is off the road, in the order a fleet manager reads them out: the
// planned trip to the garage first, "anything else" last. The car page and the
// declaration dialog both draw this list, so the order and the labels are stated
// once — same reason renting-fees.ts exists.
export const UNAVAILABILITY_REASONS: CarUnavailabilityReason[] = [
  CarUnavailabilityReason.Maintenance,
  CarUnavailabilityReason.Repair,
  CarUnavailabilityReason.Administrative,
  CarUnavailabilityReason.InternalUse,
  CarUnavailabilityReason.Other,
];

// The enum is numeric over the wire, so the key cannot be built from the value.
const LABELS: Record<number, string> = {
  [CarUnavailabilityReason.Maintenance]: 'car.unavailability.reason.maintenance',
  [CarUnavailabilityReason.Repair]: 'car.unavailability.reason.repair',
  [CarUnavailabilityReason.Administrative]: 'car.unavailability.reason.administrative',
  [CarUnavailabilityReason.InternalUse]: 'car.unavailability.reason.internalUse',
  [CarUnavailabilityReason.Other]: 'car.unavailability.reason.other',
};

export function unavailabilityReasonLabelKey(reason?: CarUnavailabilityReason): string {
  return LABELS[reason ?? CarUnavailabilityReason.Other]
    ?? LABELS[CarUnavailabilityReason.Other];
}
