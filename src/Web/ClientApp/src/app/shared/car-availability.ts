// What a car row answers when asked "can I hire this out right now?".
//
// Two independent facts decide it: the administrative status (Active /
// Maintenance / Inactive, which the agency sets) and whether a hire is running on
// the car (custody, which the bookings decide). Neither alone is the answer, so
// the screens ask through here instead of each combining them their own way.
//
// It is a statement about NOW, not about a date range: a car free today can still
// be booked next week, and only the quote endpoint (GET /api/Rentings/quote,
// which re-runs the overlap rule) can settle a given period. The wizard the Rent
// action opens does exactly that.
import { CarStatus } from '../web-api-client';

export type CarAvailability = 'available' | 'rented' | 'maintenance' | 'inactive';

// The two fields the answer needs — a CarDto satisfies it.
export interface CarAvailabilityFacts {
  status?: CarStatus;
  isOnRent?: boolean;
}

export function carAvailability(car: CarAvailabilityFacts): CarAvailability {
  // Out with a client is the most specific fact about the car: a hire that is
  // running does not stop being someone's car because the fleet marked the
  // vehicle for the garage on its return.
  if (car.isOnRent) return 'rented';
  if (car.status === CarStatus.Maintenance) return 'maintenance';
  if (car.status === CarStatus.Inactive) return 'inactive';
  return 'available';
}

export function carAvailabilityLabelKey(availability: CarAvailability): string {
  return `car.availability.${availability}`;
}

// Chip tone (see styles.scss): free to hire reads as good, out on hire as
// informational — it is business as usual, not a problem — and a car the fleet
// has withdrawn as a warning.
export function carAvailabilityClass(availability: CarAvailability): string {
  switch (availability) {
    case 'available': return 'ok';
    case 'rented': return 'info';
    case 'maintenance': return 'warn';
    default: return 'neutral';
  }
}

/** Whether the Rent action makes sense: the fleet allows it and the car is here. */
export function canRentNow(car: CarAvailabilityFacts): boolean {
  return carAvailability(car) === 'available';
}
