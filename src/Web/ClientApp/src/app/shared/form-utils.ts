// Shared helpers for the create/edit form components.

// The API serves date-only values as offset-less DateTime strings, which the
// generated client parses as LOCAL time. Formatting must therefore read the
// local date parts — going through toISOString() would convert to UTC first
// and shift the date one day earlier for users in UTC+ timezones.
export function toDateInput(date?: Date): string {
  if (!date) return '';
  const d = new Date(date);
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${month}-${day}`;
}

// A date the API GAVE us, read back as the wall clock it stands for.
//
// The app's dates are wall-clock values stamped UTC: what is sent is UTC
// midnight of the day picked (see fromDateInput), and what comes back is that
// same instant tagged 'Z' (see UtcDateTimeConverter server-side), which the
// generated client turns into a Date. So the parts to read are the UTC ones —
// local parts would report the browser's offset as part of the booking, showing
// 09:00 for a car booked out at 08:00 and saving that shift back again.
//
// toDateInput above stays local on purpose: it also formats dates the browser
// itself made ("today", a calendar cell), where local parts are the point.
export function toUtcDateInput(date?: Date): string {
  if (!date) return '';
  const d = new Date(date);
  const month = String(d.getUTCMonth() + 1).padStart(2, '0');
  const day = String(d.getUTCDate()).padStart(2, '0');
  return `${d.getUTCFullYear()}-${month}-${day}`;
}

// Same, with the hour — the value a date field bound with `withTime` holds
// (yyyy-MM-ddTHH:mm), which is how a rental period is booked.
export function toDateTimeInput(date?: Date): string {
  if (!date) return '';
  const d = new Date(date);
  const hours = String(d.getUTCHours()).padStart(2, '0');
  const minutes = String(d.getUTCMinutes()).padStart(2, '0');
  return `${toUtcDateInput(d)}T${hours}:${minutes}`;
}

// The generated client serializes Dates with toISOString(), so the Date sent
// must be UTC midnight for the server to store the exact calendar date — and the
// exact wall-clock hour in UTC when the input carries one (yyyy-MM-ddTHH:mm).
// Nothing is converted between timezones on the way in or out: what was picked is
// what is stored, and what is stored is what comes back.
export function fromDateInput(value: string): Date | undefined {
  if (!value) return undefined;
  const [datePart, timePart] = value.split('T');
  const [year, month, day] = datePart.split('-').map(Number);
  const [hours, minutes] = (timePart ?? '').split(':').map(Number);
  return new Date(Date.UTC(year, month - 1, day, hours || 0, minutes || 0));
}

// Flattens a server 400 ProblemDetails errors map into one message, handling
// both the raw HttpClient shape (err.error.errors) and the NSwag-wrapped
// exception shape (err.response as a JSON string). Returns undefined when the
// error is not a validation failure.
export function extractValidationErrors(err: any): string | undefined {
  let errors = err?.error?.errors;

  if (!errors && typeof err?.response === 'string') {
    try {
      errors = JSON.parse(err.response)?.errors;
    } catch {
      return undefined;
    }
  }

  if (!errors) return undefined;

  return Object.values(errors)
    .map(messages => (messages as string[]).join(' '))
    .join(' ');
}

// The machine-readable discriminator the API puts on a ProblemDetails (see
// CustomExceptionHandler). Several failures share one status code — 409 covers
// plan limits, booking conflicts, concurrency and reservation lifecycle — so a
// caller keys on this rather than on the status alone. Handles both the raw
// HttpClient shape (err.error.code) and the NSwag-wrapped shape (err.response as
// a JSON string).
export function errorCode(err: any): string | undefined {
  if (typeof err?.error?.code === 'string') return err.error.code;

  if (typeof err?.response === 'string') {
    try {
      const code = JSON.parse(err.response)?.code;
      return typeof code === 'string' ? code : undefined;
    } catch {
      return undefined;
    }
  }

  return undefined;
}

// True when the server rejected a write because the record changed since it was
// loaded (optimistic-concurrency conflict).
export function isConcurrencyConflict(err: any): boolean {
  return err?.status === 409 && errorCode(err) === 'concurrency_conflict';
}

// True when a reservation lifecycle action was refused because the hold had
// already moved on — someone else confirmed, cancelled or converted it while
// this user was looking at the list.
export function isInvalidTransition(err: any): boolean {
  return err?.status === 409 && errorCode(err) === 'invalid_transition';
}
