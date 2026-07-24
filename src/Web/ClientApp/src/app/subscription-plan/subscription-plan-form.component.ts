import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  SubscriptionPlansClient, SubscriptionPlanDto,
  CreateSubscriptionPlanCommand, UpdateSubscriptionPlanCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { FEATURES } from '../shared/feature-catalog';

@Component({
  selector: 'app-subscription-plan-form',
  templateUrl: './subscription-plan-form.component.html',
  styleUrls: ['./subscription-plan-form.component.css']
})
export class SubscriptionPlanFormComponent implements OnInit {
  form: FormGroup;
  planId?: number;
  saving = false;
  errorMessage = '';

  readonly features = FEATURES;
  selectedFeatures = new Set<string>();

  constructor(
    private fb: FormBuilder,
    private client: SubscriptionPlansClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      maxCars: [1, [Validators.required, Validators.min(1)]],
      maxClients: [1, [Validators.required, Validators.min(1)]],
      maxUsers: [1, [Validators.required, Validators.min(1)]],
      price: [0, [Validators.required, Validators.min(0)]]
    });
  }

  get isEdit(): boolean {
    return this.planId !== undefined;
  }

  ngOnInit() {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.planId = +idParam;
      // No get-by-id endpoint: fetch the catalog and pick the plan.
      this.client.getSubscriptionPlans().subscribe({
        next: plans => {
          const plan = (plans || []).find(p => p.id === this.planId);
          if (plan) this.populate(plan);
        },
        error: err => console.error(err)
      });
    }
  }

  private populate(dto: SubscriptionPlanDto) {
    this.form.patchValue({
      name: dto.name ?? '',
      maxCars: dto.maxCars ?? 1,
      maxClients: dto.maxClients ?? 1,
      maxUsers: dto.maxUsers ?? 1,
      price: dto.price ?? 0
    });
    this.selectedFeatures = new Set(dto.features ?? []);
  }

  toggleFeature(key: string, checked: boolean) {
    checked ? this.selectedFeatures.add(key) : this.selectedFeatures.delete(key);
  }

  isFeatureSelected(key: string): boolean {
    return this.selectedFeatures.has(key);
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    const v = this.form.value;
    const features = Array.from(this.selectedFeatures);

    if (this.isEdit) {
      const command = new UpdateSubscriptionPlanCommand({ id: this.planId, ...v, features });
      this.client.updateSubscriptionPlan(this.planId!, command).subscribe({
        next: () => this.router.navigate(['/subscription-plan']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateSubscriptionPlanCommand({ ...v, features });
      this.client.createSubscriptionPlan(command).subscribe({
        next: () => this.router.navigate(['/subscription-plan']),
        error: err => this.handleError(err)
      });
    }
  }

  private handleError(err: any) {
    this.saving = false;
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors || 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
