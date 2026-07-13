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

// The generated client serializes Dates with toISOString(), so the Date sent
// must be UTC midnight for the server to store the exact calendar date.
export function fromDateInput(value: string): Date | undefined {
  if (!value) return undefined;
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
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
