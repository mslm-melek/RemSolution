import { fromDateInput, toDateInput, toDateTimeInput, toUtcDateInput } from './form-utils';

// Dates travel as wall-clock values stamped UTC, which only works if reading and
// writing agree — a booking made for 08:00 has to come back as 08:00 whatever
// timezone the browser is in. These pin that round trip.
describe('form-utils dates', () => {
  it('sends the day picked as UTC midnight', () => {
    expect(fromDateInput('2026-08-14')!.toISOString()).toBe('2026-08-14T00:00:00.000Z');
  });

  it('sends the hour picked as that hour in UTC', () => {
    expect(fromDateInput('2026-08-14T08:00')!.toISOString()).toBe('2026-08-14T08:00:00.000Z');
  });

  it('reads a date-and-time back as the hour that was picked', () => {
    expect(toDateTimeInput(new Date('2026-08-14T08:00:00Z'))).toBe('2026-08-14T08:00');
  });

  it('round-trips a date-and-time unchanged', () => {
    const value = '2026-08-14T17:45';

    expect(toDateTimeInput(fromDateInput(value))).toBe(value);
  });

  it('keeps a late-evening booking on its own day', () => {
    expect(toUtcDateInput(new Date('2026-08-14T23:30:00Z'))).toBe('2026-08-14');
  });

  it('still reads a browser-made date from its local parts', () => {
    const today = new Date(2026, 7, 14, 9, 30);

    expect(toDateInput(today)).toBe('2026-08-14');
  });

  it('has nothing to say about an empty value', () => {
    expect(fromDateInput('')).toBeUndefined();
    expect(toDateTimeInput(undefined)).toBe('');
  });
});
