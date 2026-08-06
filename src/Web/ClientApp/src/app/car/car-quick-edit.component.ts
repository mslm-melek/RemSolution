import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import {
  BranchDto, BranchesClient, CarDto, CarsClient, CarStatus, UpdateCarCommand
} from '../web-api-client';
import { extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';

export interface CarQuickEditData {
  carId: number;
}

/**
 * The four fields on a car that go stale between hires — whether it is on the
 * road, what it costs a day, where it is based, what the odometer reads — edited
 * from the car's own page instead of on the whole-record form.
 *
 * Those four and no more: they are the ones a counter changes without looking at
 * the vehicle, and everything else (the model, the plate, the photos) is a
 * correction to the record rather than a fact about today. The form at
 * /car/:id/edit still owns all of it, and this panel links to it.
 *
 * The car is re-read on open rather than passed in, for the reason the return
 * dialog does the same: the concurrency token on a page that has been sitting
 * open is a stale one, and saving with it would 409 on a car nobody else touched.
 * The re-read also means the panel cannot save a field the page never showed —
 * UpdateCarCommand replaces the WHOLE car (see its handler), so the untouched
 * fields have to be sent back as they currently are, not as they were on load.
 */
@Component({
  selector: 'app-car-quick-edit',
  templateUrl: './car-quick-edit.component.html',
  styleUrls: ['./car-quick-edit.component.css']
})
export class CarQuickEditComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<CarQuickEditData>(MAT_DIALOG_DATA);

  car?: CarDto;
  branches: BranchDto[] = [];
  form: FormGroup;

  loading = true;
  saving = false;
  errorMessage = '';

  statuses = [
    { value: CarStatus.Active, labelKey: 'enums.carStatus.active' },
    { value: CarStatus.Maintenance, labelKey: 'enums.carStatus.maintenance' },
    { value: CarStatus.Inactive, labelKey: 'enums.carStatus.inactive' }
  ];

  constructor(
    private fb: FormBuilder,
    private cars: CarsClient,
    private branchesClient: BranchesClient,
    private dialog: MatDialogRef<CarQuickEditComponent, boolean>
  ) {
    this.form = this.fb.group({
      status: [CarStatus.Active, Validators.required],
      // The same floor the whole-record form applies, so the two screens refuse
      // the same values rather than one of them deferring to the server.
      dailyRate: [null, Validators.min(0.01)],
      branchId: [null],
      mileage: [null, Validators.min(0)]
    });
  }

  ngOnInit() {
    this.cars.getCarById(this.data.carId).subscribe({
      next: car => {
        this.car = car;
        this.form.patchValue({
          status: car.status ?? CarStatus.Active,
          dailyRate: car.dailyRate?.amount ?? null,
          branchId: car.branchId ?? null,
          mileage: car.mileage ?? null
        });
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.handleError(err);
      }
    });

    // The branch list is small and cached by nobody; a panel that opened without
    // it would show the car's branch as an empty select.
    this.branchesClient.getBranches().subscribe({
      next: branches => this.branches = branches || [],
      error: err => console.error(err)
    });
  }

  /** Make and model, as the page behind the panel names the same car. */
  get carName(): string {
    return [this.car?.brandName, this.car?.modelName].filter(Boolean).join(' ');
  }

  get currency(): string | undefined {
    return this.car?.dailyRate?.currency;
  }

  save() {
    if (this.form.invalid || !this.car || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const value = this.form.value;

    // Every field, not just the four: the command assigns the whole car, so
    // anything left out would be cleared. The four come from the panel, the rest
    // from the car as it was read a moment ago.
    const command = new UpdateCarCommand({
      id: this.car.id,
      rowVersion: this.car.rowVersion,
      modelId: this.car.modelId,
      branchId: value.branchId ?? undefined,
      status: value.status,
      dailyRate: value.dailyRate ?? undefined,
      firstCirculationDate: this.car.firstCirculationDate,
      color: this.car.color || undefined,
      power: this.car.power ?? undefined,
      fuelType: this.car.fuelType ?? undefined,
      mileage: value.mileage === null || value.mileage === '' ? undefined : Number(value.mileage)
    });

    this.cars.updateCar(this.car.id!, command).subscribe({
      next: () => this.dialog.close(true),
      error: err => {
        this.saving = false;
        this.handleError(err);
      }
    });
  }

  cancel() {
    this.dialog.close(false);
  }

  private handleError(err: any) {
    if (isConcurrencyConflict(err)) {
      this.errorMessage = this.transloco.translate('car.concurrency');
      return;
    }

    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
