import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ModelCarsClient, ModelCarDto, CreateModelCarCommand, UpdateModelCarCommand,
  BrandsClient, BrandDto
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-model-car-form',
  templateUrl: './model-car-form.component.html',
  styleUrls: ['./model-car-form.component.css']
})
export class ModelCarFormComponent implements OnInit {
  form: FormGroup;
  brands: BrandDto[] = [];
  modelCarId?: number;
  saving = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private client: ModelCarsClient,
    private brandsClient: BrandsClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      brandId: [null, Validators.required]
    });
  }

  get isEdit(): boolean {
    return this.modelCarId !== undefined;
  }

  ngOnInit() {
    this.brandsClient.getBrands().subscribe({
      next: brands => this.brands = brands || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.modelCarId = +idParam;
      this.client.getModelCarById(this.modelCarId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
    }
  }

  private populate(dto: ModelCarDto) {
    this.form.patchValue({
      name: dto.name ?? '',
      brandId: dto.brandId ?? null
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
      const command = new UpdateModelCarCommand({
        id: this.modelCarId,
        name: v.name,
        brandId: v.brandId
      });
      this.client.updateModelCar(this.modelCarId!, command).subscribe({
        next: () => this.router.navigate(['/model-car']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateModelCarCommand({
        name: v.name,
        brandId: v.brandId
      });
      this.client.createModelCar(command).subscribe({
        next: () => this.router.navigate(['/model-car']),
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
