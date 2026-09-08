import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { AgencyReportKind, CreateMyReportCommand, MarketplaceClient } from '../web-api-client';
import { extractValidationErrors, extractProblemDetail } from '../shared/form-utils';
import { REPORT_KINDS, reportKindLabelKey } from '../shared/agency-reports';

export interface ReportDialogData {
  // Exactly one of the two, mirroring the command: a report is about one booking.
  reservationId?: number;
  rentingId?: number;
  agencyName?: string;
  bookingLabel?: string;
  // Pre-selects "the agency cancelled" — the server refuses that reason on a
  // booking that was not, so it is offered only when it is true.
  cancelledByAgency?: boolean;
}

// The customer's complaint. A dialog rather than a prompt() because it asks two
// things — what kind of problem, and what happened — and because the customer
// has to be told what this is before writing it: the platform arbitrates, and no
// money moves either way.
@Component({
  selector: 'app-report-dialog',
  templateUrl: './report-dialog.component.html',
  styleUrls: ['./report-dialog.component.css']
})
export class ReportDialogComponent {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<ReportDialogData>(MAT_DIALOG_DATA);

  form: FormGroup;
  saving = false;
  errorMessage = '';

  readonly kinds = REPORT_KINDS;
  readonly kindLabelKey = reportKindLabelKey;

  constructor(
    private fb: FormBuilder,
    private client: MarketplaceClient,
    private dialog: MatDialogRef<ReportDialogComponent, boolean>
  ) {
    this.form = this.fb.group({
      kind: [
        this.data.cancelledByAgency
          ? AgencyReportKind.CancelledBooking
          : AgencyReportKind.ServiceQuality,
        Validators.required
      ],
      message: ['', [Validators.required, Validators.maxLength(2000)]]
    });
  }

  /** "The agency cancelled" is only a truthful reason when it did. */
  offers(kind: AgencyReportKind): boolean {
    return kind !== AgencyReportKind.CancelledBooking || !!this.data.cancelledByAgency;
  }

  submit() {
    if (this.form.invalid || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    this.client.reportAgency(new CreateMyReportCommand({
      reservationId: this.data.reservationId,
      rentingId: this.data.rentingId,
      kind: this.form.value.kind,
      message: this.form.value.message
    })).subscribe({
      next: () => this.dialog.close(true),
      error: err => {
        this.saving = false;
        this.errorMessage = extractValidationErrors(err)
          ?? extractProblemDetail(err)
          ?? this.transloco.translate('reports.customer.sendFailed');
      }
    });
  }

  close() {
    this.dialog.close(false);
  }
}
