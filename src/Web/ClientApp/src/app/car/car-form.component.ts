import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CarsClient, CarDto, CreateCarCommand, UpdateCarCommand,
  FuelType, ModelCarsClient, ModelCarDto
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-car-form',
  templateUrl: './car-form.component.html',
  styleUrls: ['./car-form.component.css']
})
export class CarFormComponent implements OnInit {
  form: FormGroup;
  models: ModelCarDto[] = [];
  carId?: number;
  saving = false;
  errorMessage = '';

  fuelTypes = [
    { value: FuelType.Gasoline, label: 'Gasoline' },
    { value: FuelType.Diesel, label: 'Diesel' }
  ];

  constructor(
    private fb: FormBuilder,
    private client: CarsClient,
    private modelCarsClient: ModelCarsClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      matricule: ['', [Validators.required, Validators.maxLength(200)]],
      modelId: [null, Validators.required],
      firstCirculationDate: ['', Validators.required],
      color: [''],
      power: [null],
      fuelType: [null],
      imageUrl: ['']
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
    }
  }

  private populate(dto: CarDto) {
    this.form.patchValue({
      matricule: dto.matricule ?? '',
      modelId: dto.modelId ?? null,
      firstCirculationDate: toDateInput(dto.firstCirculationDate),
      color: dto.color ?? '',
      power: dto.power ?? null,
      fuelType: dto.fuelType ?? null,
      imageUrl: dto.imageUrl ?? ''
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
        modelId: v.modelId,
        firstCirculationDate: fromDateInput(v.firstCirculationDate),
        color: v.color || undefined,
        imageUrl: v.imageUrl || undefined,
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
        firstCirculationDate: fromDateInput(v.firstCirculationDate),
        color: v.color || undefined,
        imageUrl: v.imageUrl || undefined,
        power: v.power ?? undefined,
        fuelType: v.fuelType ?? undefined
      });
      this.client.createCar(command).subscribe({
        next: () => this.router.navigate(['/car']),
        error: err => this.handleError(err)
      });
    }
  }

  private handleError(err: any) {
    this.saving = false;

    const validationErrors = extractValidationErrors(err);
    if (validationErrors) {
      this.errorMessage = validationErrors;
    } else {
      this.errorMessage = 'An unexpected error occurred. Please try again.';
      console.error(err);
    }
  }
}
