import { formatDate } from '@angular/common';
import { NotificationDto, NotificationKind } from '../web-api-client';

// How a notification looks and reads. Mirrors the server's Domain enums — the
// kind decides the icon and the colour, and the row's own messageKey decides the
// sentence (see Notification.MessageKey for why those are two things).

export type NotificationSeverity = 'danger' | 'warn' | 'info';

export interface NotificationLook {
  icon: string;
  severity: NotificationSeverity;
}

// Severity is derived here rather than stored on the row: "a car is late" is
// always the loud one and "a pickup is coming up" always the quiet one, so it is
// a property of the kind, not data worth a column.
const LOOKS: Record<NotificationKind, NotificationLook> = {
  [NotificationKind.CarExpenseDue]: { icon: 'build', severity: 'warn' },
  [NotificationKind.RentingOverdue]: { icon: 'running_with_errors', severity: 'danger' },
  [NotificationKind.ReservationUpcoming]: { icon: 'event_available', severity: 'info' },
  [NotificationKind.RentingStartingSoon]: { icon: 'outgoing_mail', severity: 'info' },
  [NotificationKind.RentingEndingSoon]: { icon: 'outgoing_mail', severity: 'info' },
  [NotificationKind.RentingLateNotice]: { icon: 'outgoing_mail', severity: 'warn' }
};

const FALLBACK: NotificationLook = { icon: 'notifications', severity: 'info' };

export function notificationLook(kind: NotificationKind | undefined): NotificationLook {
  return (kind !== undefined && LOOKS[kind]) || FALLBACK;
}

/**
 * The translation key for a row's wording. The suffix is the server's stored
 * MessageKey, so these keys are a contract shared with the resx mail templates —
 * see NotificationMessages on the server before renaming one.
 */
export function notificationMessageKey(notification: NotificationDto): string {
  return `notifications.message.${notification.messageKey}`;
}

/**
 * The row's interpolation values, ready for Transloco.
 *
 * Date arguments arrive as ISO `yyyy-MM-dd` — the server cannot know which
 * language will read the row, so it stores them round-trippable and leaves the
 * formatting to whoever renders (the mail composer does the same thing on its
 * side). Anything named with a `Date` suffix is therefore formatted here, in the
 * user's locale.
 */
export function notificationArgs(
  notification: NotificationDto, locale: string): Record<string, string> {
  const args = notification.args ?? {};
  const formatted: Record<string, string> = {};

  for (const name of Object.keys(args)) {
    const value = args[name];

    formatted[name] = name.endsWith('Date') && value
      ? formatDateArg(value, locale)
      : value;
  }

  return formatted;
}

// Unparseable content is shown as it came rather than dropped: a raw date in the
// sentence beats a hole in it.
function formatDateArg(isoDate: string, locale: string): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(isoDate)) return isoDate;

  const [year, month, day] = isoDate.split('-').map(Number);

  // Built from the parts rather than parsed: `new Date('2026-08-14')` is UTC
  // midnight, which renders as the day before in any negative offset.
  return formatDate(new Date(year, month - 1, day), 'mediumDate', locale);
}
