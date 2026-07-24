import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CarsClient, CarDto, CreateCarCommand, UpdateCarCommand,
  FuelType, CarStatus, ModelCarsClient, ModelCarDto,
  BranchesClient, BranchDto, CarImageDto, ImageProcessingStatus, FileParameter
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';

@Component({
  selector: 'app-car-form',
  templateUrl: './car-form.component.html',
  styleUrls: ['./car-form.component.css']
})
export class CarFormComponent implements OnInit, OnDestroy {
  form: FormGroup;
  models: ModelCarDto[] = [];
  branches: BranchDto[] = [];
  carId?: number;
  saving = false;
  errorMessage = '';

  // Currency is agency-scoped and set server-side; surfaced read-only next to
  // the daily-rate amount once the car (and thus its currency) is known.
  currency?: string;

  // Optimistic-concurrency token read with the car and echoed back on update,
  // so a save that lost a race is rejected with 409 instead of clobbering.
  private rowVersion?: string;

  // Gallery (CarImage): many images, each with a generated thumbnail. Available
  // only once the car exists, like the legacy single photo.
  images: CarImageDto[] = [];
  uploadingImage = false;
  // Object URL for the just-picked file, shown instantly while the upload and
  // out-of-band thumbnail generation happen; revoked once done.
  previewUrl?: string;

  fuelTypes = [
    { value: FuelType.Gasoline, label: 'Gasoline' },
    { value: FuelType.Diesel, label: 'Diesel' }
  ];

  statuses = [
    { value: CarStatus.Active, label: 'Active' },
    { value: CarStatus.Maintenance, label: 'Maintenance' },
    { value: CarStatus.Inactive, label: 'Inactive' }
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
      fuelType: [null]
    });
  }

  get isEdit(): boolean {
    return this.carId !== undefined;
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
      // The matricule identifies the vehicle and is not part of
      // UpdateCarCommand, so it stays read-only when editing.
      this.form.get('matricule')!.disable();
      this.client.getCarById(this.carId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
      this.loadImages();
    }
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
      fuelType: dto.fuelType ?? null
    });
    this.currency = dto.dailyRate?.currency;
    this.rowVersion = dto.rowVersion;
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
        fuelType: v.fuelType ?? undefined
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
        fuelType: v.fuelType ?? undefined
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
    // Instant local preview while the server stores the original and generates
    // the thumbnail/medium out of band.
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

  // Prefer the generated thumbnail; fall back to the original while derivatives
  // are still being produced.
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
      this.errorMessage =
        'This car was reloaded by another user since you opened it. Reload the page to get the latest version, then re-apply your changes.';
      return;
    }

    const validationErrors = extractValidationErrors(err);
    if (validationErrors) {
      this.errorMessage = validationErrors;
    } else {
      this.errorMessage = 'An unexpected error occurred. Please try again.';
      console.error(err);
    }
  }
}
