import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CarsClient, CarDto, CreateCarCommand, UpdateCarCommand,
  FuelType, CarStatus, ModelCarsClient, ModelCarDto,
  BranchesClient, BranchDto, CarImageDto, ImageProcessingStatus, FileParameter,
  CarExpenseScheduleDto, CarExpenseScheduleInput
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-car-form',
  templateUrl: './car-form.component.html',
  styleUrls: ['./car-form.component.css']
})
export class CarFormComponent implements OnInit, OnDestroy {
  // Error banners are plain strings, so they are translated imperatively.
  private readonly transloco = inject(TranslocoService);
  form: FormGroup;
  models: ModelCarDto[] = [];
  branches: BranchDto[] = [];
  carId?: number;
  saving = false;
  errorMessage = '';

  // Agency-scoped and set server-side; shown read-only beside the daily rate.
  currency?: string;

  // Optimistic-concurrency token, echoed back on update so a lost race 409s.
  private rowVersion?: string;

  // One row per notifiable expense type. The DTOs are kept beside the form array
  // for the fleet-wide labels; the editable figures live in the array.
  // Without Expense.Read the call 403s, and the section stays hidden and unsent —
  // an update that omits the schedules leaves them alone.
  scheduleTypes: CarExpenseScheduleDto[] = [];
  schedulesLoaded = false;

  // Gallery (CarImage), available only once the car exists.
  images: CarImageDto[] = [];
  uploadingImage = false;
  // Object URL for the picked file, shown while the upload and resize happen.
  previewUrl?: string;

  fuelTypes = [
    { value: FuelType.Gasoline, labelKey: 'enums.fuelType.gasoline' },
    { value: FuelType.Diesel, labelKey: 'enums.fuelType.diesel' }
  ];

  statuses = [
    { value: CarStatus.Active, labelKey: 'enums.carStatus.active' },
    { value: CarStatus.Maintenance, labelKey: 'enums.carStatus.maintenance' },
    { value: CarStatus.Inactive, labelKey: 'enums.carStatus.inactive' }
  ];

  constructor(
    private fb: FormBuilder,
    private client: CarsClient,
    private modelCarsClient: ModelCarsClient,
    private branchesClient: BranchesClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      matricule: ['', [Validators.required, Validators.maxLength(200)]],
      modelId: [null, Validators.required],
      branchId: [null],
      status: [CarStatus.Active, Validators.required],
      dailyRate: [null, Validators.min(0.01)],
      firstCirculationDate: ['', Validators.required],
      color: [''],
      power: [null],
      fuelType: [null],
      // The car's odometer, which a booking on it takes as its pickup reading.
      mileage: [null, Validators.min(0)],
      expenseSchedules: this.fb.array([])
    });
  }

  get isEdit(): boolean {
    return this.carId !== undefined;
  }

  get scheduleRows(): FormArray {
    return this.form.get('expenseSchedules') as FormArray;
  }

  ngOnInit() {
    this.modelCarsClient.getAllModelCars().subscribe({
      next: models => this.models = models || [],
      error: err => console.error(err)
    });

    this.branchesClient.getBranches().subscribe({
      next: branches => this.branches = branches || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.carId = +idParam;
      // Not part of UpdateCarCommand, so read-only when editing.
      this.form.get('matricule')!.disable();
      this.client.getCarById(this.carId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
      this.loadImages();
    }

    // Asked without a car too: a new car can be given intervals as it is created.
    this.client.getCarExpenseSchedules(this.carId ?? undefined).subscribe({
      next: rows => this.buildScheduleRows(rows || []),
      // Silent: reaching this form without Expense.Read is normal, not an error.
      error: () => this.schedulesLoaded = false
    });
  }

  ngOnDestroy() {
    this.clearPreview();
  }

  private populate(dto: CarDto) {
    this.form.patchValue({
      matricule: dto.matricule ?? '',
      modelId: dto.modelId ?? null,
      branchId: dto.branchId ?? null,
      status: dto.status ?? CarStatus.Active,
      dailyRate: dto.dailyRate?.amount ?? null,
      firstCirculationDate: toDateInput(dto.firstCirculationDate),
      color: dto.color ?? '',
      power: dto.power ?? null,
      fuelType: dto.fuelType ?? null,
      mileage: dto.mileage ?? null
    });
    this.currency = dto.dailyRate?.currency;
    this.rowVersion = dto.rowVersion;
  }

  private buildScheduleRows(rows: CarExpenseScheduleDto[]) {
    this.scheduleTypes = rows;
    this.scheduleRows.clear();

    for (const row of rows) {
      this.scheduleRows.push(this.fb.group({
        expenseTypeId: [row.expenseTypeId],
        // Off means "this car follows the fleet rule", which most cars do.
        linked: [row.isLinked],
        afterKilometer: [row.afterKilometer ?? null, Validators.min(1)],
        afterMonth: [row.afterMonth ?? null, Validators.min(1)],
        leadKilometers: [row.leadKilometers ?? null, Validators.min(0)],
        leadDays: [row.leadDays ?? null, Validators.min(0)],
        lastDoneMileage: [row.lastDoneMileage ?? null, Validators.min(0)],
        lastDoneOn: [toDateInput(row.lastDoneOn)]
      }));
    }

    this.schedulesLoaded = true;
  }

  /** The type's fleet-wide figures, shown beside the row as the fallback. */
  scheduleType(index: number): CarExpenseScheduleDto | undefined {
    return this.scheduleTypes[index];
  }

  isScheduleLinked(index: number): boolean {
    return !!this.scheduleRows.at(index).get('linked')?.value;
  }

  /** Unticking clears the figures, so stale numbers cannot come back on re-ticking. */
  onScheduleLinkedChange(index: number, linked: boolean) {
    if (!linked) {
      this.scheduleRows.at(index).patchValue({
        afterKilometer: null, afterMonth: null,
        leadKilometers: null, leadDays: null,
        lastDoneMileage: null, lastDoneOn: ''
      });
    }
  }

  private scheduleCommand(): CarExpenseScheduleInput[] | undefined {
    if (!this.schedulesLoaded) {
      return undefined;
    }

    return this.scheduleRows.controls
      .filter(row => row.get('linked')!.value)
      .map(row => {
        const v = row.value;
        return new CarExpenseScheduleInput({
          expenseTypeId: v.expenseTypeId,
          afterKilometer: v.afterKilometer ?? undefined,
          afterMonth: v.afterMonth ?? undefined,
          leadKilometers: v.leadKilometers ?? undefined,
          leadDays: v.leadDays ?? undefined,
          lastDoneMileage: v.lastDoneMileage ?? undefined,
          lastDoneOn: v.lastDoneOn ? fromDateInput(v.lastDoneOn) : undefined
        });
      });
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    const v = this.form.value;

    if (this.isEdit) {
      const command = new UpdateCarCommand({
        id: this.carId,
        rowVersion: this.rowVersion,
        modelId: v.modelId,
        branchId: v.branchId ?? undefined,
        status: v.status,
        dailyRate: v.dailyRate ?? undefined,
        firstCirculationDate: fromDateInput(v.firstCirculationDate),
        color: v.color || undefined,
        power: v.power ?? undefined,
        fuelType: v.fuelType ?? undefined,
        mileage: v.mileage ?? undefined,
        expenseSchedules: this.scheduleCommand()
      });
      this.client.updateCar(this.carId!, command).subscribe({
        next: () => this.router.navigate(['/car']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateCarCommand({
        matricule: v.matricule,
        modelId: v.modelId,
        branchId: v.branchId ?? undefined,
        status: v.status,
        dailyRate: v.dailyRate ?? undefined,
        firstCirculationDate: fromDateInput(v.firstCirculationDate),
        color: v.color || undefined,
        power: v.power ?? undefined,
        fuelType: v.fuelType ?? undefined,
        mileage: v.mileage ?? undefined,
        expenseSchedules: this.scheduleCommand()
      });
      this.client.createCar(command).subscribe({
        next: () => this.router.navigate(['/car']),
        error: err => this.handleError(err)
      });
    }
  }

  private loadImages() {
    if (!this.carId) return;
    this.client.getCarImages(this.carId).subscribe({
      next: images => this.images = images || [],
      error: err => console.error(err)
    });
  }

  onImageSelected(input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !this.carId) return;

    this.uploadingImage = true;
    this.errorMessage = '';
    // Local preview while the server stores and resizes out of band.
    this.setPreview(URL.createObjectURL(file));

    const parameter: FileParameter = { data: file, fileName: file.name };
    this.client.uploadCarImage(this.carId, parameter).subscribe({
      next: () => {
        this.clearPreview();
        this.uploadingImage = false;
        this.loadImages();
      },
      error: err => {
        this.clearPreview();
        this.uploadingImage = false;
        this.handleError(err);
      }
    });
  }

  setPrimary(image: CarImageDto) {
    if (!this.carId || !image.id || image.isPrimary) return;
    this.client.setPrimaryCarImage(this.carId, image.id).subscribe({
      next: () => this.loadImages(),
      error: err => this.handleError(err)
    });
  }

  deleteImage(image: CarImageDto) {
    if (!this.carId || !image.id) return;
    this.client.deleteCarImage(this.carId, image.id).subscribe({
      next: () => this.loadImages(),
      error: err => this.handleError(err)
    });
  }

  // Prefer the thumbnail; fall back while derivatives are still being produced.
  thumbnailFor(image: CarImageDto): string | undefined {
    return image.thumbnailUrl ?? image.originalUrl ?? undefined;
  }

  isProcessing(image: CarImageDto): boolean {
    return image.processingStatus === ImageProcessingStatus.Pending
      || image.processingStatus === ImageProcessingStatus.Processing;
  }

  isFailed(image: CarImageDto): boolean {
    return image.processingStatus === ImageProcessingStatus.Failed;
  }

  private setPreview(url: string) {
    this.clearPreview();
    this.previewUrl = url;
  }

  private clearPreview() {
    if (this.previewUrl) {
      URL.revokeObjectURL(this.previewUrl);
      this.previewUrl = undefined;
    }
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage = this.transloco.translate('car.concurrency');
      return;
    }

    const validationErrors = extractValidationErrors(err);
    if (validationErrors) {
      this.errorMessage = validationErrors;
    } else {
      this.errorMessage = this.transloco.translate('common.unexpectedError');
      console.error(err);
    }
  }
}
