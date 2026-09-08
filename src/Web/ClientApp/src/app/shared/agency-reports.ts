import { AgencyReportKind, AgencyReportStatus } from '../web-api-client';

// What a report is CALLED, in one place — four screens show the same rows (the
// platform's queue, the agency's list, the customer's list, the report dialog)
// and they must not word a verdict differently.
//
// Keyed by the enum's numeric value, like the notification kinds: the labels
// live under `reports.kinds` / `reports.statuses` in the translation files.
export function reportKindLabelKey(kind?: AgencyReportKind): string {
  return kind === undefined || kind === null ? '' : `reports.kinds.${kind}`;
}

export function reportStatusLabelKey(status?: AgencyReportStatus): string {
  return status === undefined || status === null ? '' : `reports.statuses.${status}`;
}

// Tone class for the global `.chip` (see styles.scss). Upheld is the bad news —
// for the agency, which is who reads this most — waiting is work outstanding,
// and a dismissal is the quiet end of it.
export function reportStatusTone(status?: AgencyReportStatus): string {
  switch (status) {
    case AgencyReportStatus.Upheld:
      return 'danger';
    case AgencyReportStatus.Open:
      return 'warn';
    default:
      return 'neutral';
  }
}

// Every kind the customer may pick, in the order the dialog offers them.
export const REPORT_KINDS: AgencyReportKind[] = [
  AgencyReportKind.CancelledBooking,
  AgencyReportKind.ServiceQuality,
  AgencyReportKind.Billing,
  AgencyReportKind.Vehicle,
  AgencyReportKind.Other
];
