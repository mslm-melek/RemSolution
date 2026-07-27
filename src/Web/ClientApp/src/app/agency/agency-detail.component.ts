import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  AgenciesClient, AgencyDto, AgencyFeatureDto,
  UsersClient, AgencyUserDto,
  AgencySubscriptionsClient, AgencySubscriptionDto, AgencyUsageDto,
  AssignAgencySubscriptionCommand, UpdateAgencySubscriptionCommand, SetAgencyUserActiveCommand,
  SubscriptionPlansClient, SubscriptionPlanDto, SubscriptionStatus
} from '../web-api-client';
import { fromDateInput, extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-agency-detail',
  templateUrl: './agency-detail.component.html',
  styleUrls: ['./agency-detail.component.css']
})
export class AgencyDetailComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  agencyId!: number;
  agency?: AgencyDto;

  users: AgencyUserDto[] = [];
  usersColumns: string[] = ['userName', 'role', 'status', 'permissions', 'actions'];

  subscriptions: AgencySubscriptionDto[] = [];
  usage?: AgencyUsageDto;
  plans: SubscriptionPlanDto[] = [];
  assignForm: FormGroup;

  features: AgencyFeatureDto[] = [];

  // One-time credentials shown when assigning the first plan bootstraps an admin.
  createdAdmin: { userName?: string; password?: string } | null = null;

  errorMessage = '';

  readonly SubscriptionStatus = SubscriptionStatus;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private agenciesClient: AgenciesClient,
    private usersClient: UsersClient,
    private subscriptionsClient: AgencySubscriptionsClient,
    private plansClient: SubscriptionPlansClient
  ) {
    this.assignForm = this.fb.group({
      planId: [null, Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required]
    });
  }

  ngOnInit() {
    this.agencyId = +this.route.snapshot.paramMap.get('id')!;
    this.loadAgency();
    this.loadUsers();
    this.loadSubscription();
    this.loadFeatures();
    this.plansClient.getSubscriptionPlans().subscribe({
      next: p => this.plans = p || [],
      error: err => console.error(err)
    });
  }

  private loadAgency() {
    this.agenciesClient.getAgencyById(this.agencyId).subscribe({
      next: a => this.agency = a,
      error: err => console.error(err)
    });
  }

  // --- Users ---
  loadUsers() {
    this.usersClient.getAgencyUsers(this.agencyId).subscribe({
      next: u => this.users = u || [],
      error: err => console.error(err)
    });
  }

  toggleActive(user: AgencyUserDto) {
    if (!user.id) return;
    const activate = !!user.isLockedOut;
    const verb = this.transloco.translate(activate ? 'common.reactivate' : 'common.deactivate');
    if (!confirm(this.transloco.translate('agency.confirmToggleUser', { verb, user: user.userName }))) return;

    const command = new SetAgencyUserActiveCommand({ userId: user.id, isActive: activate });
    this.usersClient.setAgencyUserActive(user.id, command).subscribe({
      next: () => this.loadUsers(),
      error: err => console.error(err)
    });
  }

  // --- Subscription & usage ---
  loadSubscription() {
    this.subscriptionsClient.getAgencySubscriptions(this.agencyId).subscribe({
      next: s => this.subscriptions = s || [],
      error: err => console.error(err)
    });
    this.subscriptionsClient.getAgencyUsage(this.agencyId).subscribe({
      next: u => this.usage = u,
      error: err => console.error(err)
    });
  }

  percent(used?: number, max?: number): number {
    if (!max || max <= 0) return 0;
    return Math.min(100, Math.round(((used ?? 0) / max) * 100));
  }

  assignPlan() {
    if (this.assignForm.invalid) {
      this.assignForm.markAllAsTouched();
      return;
    }
    this.errorMessage = '';
    const v = this.assignForm.value;
    const command = new AssignAgencySubscriptionCommand({
      agencyId: this.agencyId,
      planId: v.planId,
      startDate: fromDateInput(v.startDate),
      endDate: fromDateInput(v.endDate)
    });
    this.subscriptionsClient.assignAgencySubscription(command).subscribe({
      next: result => {
        this.assignForm.reset();
        // If this assignment bootstrapped the agency's first admin, surface the
        // one-time credentials so they can be handed over.
        if (result?.adminUserName) {
          this.createdAdmin = { userName: result.adminUserName, password: result.adminTemporaryPassword };
        }
        this.loadSubscription();
        this.loadUsers();
      },
      error: err => this.handleError(err)
    });
  }

  changeStatus(sub: AgencySubscriptionDto, status: SubscriptionStatus) {
    if (!sub.id) return;
    const command = new UpdateAgencySubscriptionCommand({ id: sub.id, status, endDate: sub.endDate });
    this.subscriptionsClient.updateAgencySubscription(sub.id, command).subscribe({
      next: () => this.loadSubscription(),
      error: err => this.handleError(err)
    });
  }

  // Returns a transloco key; the template pipes it so a language switch
  // re-renders the value.
  statusLabelKey(status?: SubscriptionStatus): string {
    switch (status) {
      case SubscriptionStatus.Active: return 'enums.subscriptionStatus.active';
      case SubscriptionStatus.Suspended: return 'enums.subscriptionStatus.suspended';
      case SubscriptionStatus.Expired: return 'enums.subscriptionStatus.expired';
      default: return '';
    }
  }

  // --- Features ---
  loadFeatures() {
    this.agenciesClient.getAgencyFeatures(this.agencyId).subscribe({
      next: f => this.features = f || [],
      error: err => console.error(err)
    });
  }

  toggleFeature(feature: AgencyFeatureDto, enabled: boolean) {
    this.errorMessage = '';
    this.agenciesClient.setAgencyFeature(this.agencyId, {
      agencyId: this.agencyId,
      feature: feature.feature,
      enabled
    } as any).subscribe({
      next: () => feature.enabled = enabled,
      error: err => {
        this.handleError(err);
        this.loadFeatures(); // revert to the server truth on failure
      }
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors || 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
