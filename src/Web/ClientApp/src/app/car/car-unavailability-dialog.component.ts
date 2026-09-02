import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import {
  CarsClient, CarUnavailabilityDto, CarUnavailabilityReason,
  CreateCarUnavailabilityCommand, UpdateCarUnavailabilityCommand
} from '../web-api-client';
import {
  extractValidationErrors, extractProblemDetail, fromDateInput, toDateInput
} from '../shared/form-utils';
import { UNAVAILABILITY_REASONS, unavailabilityReasonLabelKey } from '../shared/car-unavailability';

export interface CarUnavailabilityDialogData {
  carId: number;
  carLabel?: string;
  /** The block being edited; absent declares a new one. */
  block?: CarUnavailabilityDto;
}

// Declares a car off the road for a stretch of dates, or moves a stretch already
// declared. The dates compete with bookings, so the server refuses a period that
// overlaps a hire or a hold — that refusal is the useful outcome, not an error to
// hide: it tells the desk there is a booking to move first.
@Component({
  selector: 'app-car-unavailability-dialog',
  templateUrl: './car-unavailability-dialog.component.html',
  styleUrls: ['./car-unavailability-dialog.component.css']
})
export class CarUnavailabilityDialogComponent {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<CarUnavailabilityDialogData>(MAT_DIALOG_DATA);

  readonly reasons = UNAVAILABILITY_REASONS;
  readonly reasonLabelKey = unavailabilityReasonLabelKey;

  form: FormGroup;
  saving = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private cars: CarsClient,
    private dialog: MatDialogRef<CarUnavailabilityDialogComponent, boolean>
  ) {
    const block = this.data.block;

    this.form = this.fb.group({
      startDate: [toDateInput(block?.startDate), Validators.required],
      endDate: [toDateInput(block?.endDate), Validators.required],
      reason: [block?.reason ?? CarUnavailabilityReason.Maintenance, Validators.required],
      note: [block?.note ?? '', Validators.maxLength(500)]
    });
  }

  get isEdit(): boolean {
    return this.data.block !== undefined;
  }

  /** The server refuses it too; saying so here saves the round-trip. */
  get periodInvalid(): boolean {
    const { startDate, endDate } = this.form.value;
    return !!startDate && !!endDate && endDate <= startDate;
  }

  submit() {
    if (this.form.invalid || this.periodInvalid || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const v = this.form.value;
    const startDate = fromDateInput(v.startDate)!;
    const endDate = fromDateInput(v.endDate)!;

    // Typed as unknown: the create returns the new id and the update returns
    // nothing, and neither result is used — only that it succeeded.
    const request: Observable<unknown> = this.isEdit
      ? this.cars.updateCarUnavailability(this.data.block!.id!, new UpdateCarUnavailabilityCommand({
          id: this.data.block!.id,
          startDate,
          endDate,
          reason: v.reason,
          note: v.note?.trim() || undefined
        }))
      : this.cars.createCarUnavailability(this.data.carId, new CreateCarUnavailabilityCommand({
          carId: this.data.carId,
          startDate,
          endDate,
          reason: v.reason,
          note: v.note?.trim() || undefined
        }));

    request.subscribe({
      next: () => this.dialog.close(true),
      error: err => {
        this.saving = false;
        this.errorMessage = extractValidationErrors(err)
          ?? extractProblemDetail(err)
          ?? this.transloco.translate('common.unexpectedError');
      }
    });
  }

  cancel() {
    this.dialog.close(false);
  }
}
