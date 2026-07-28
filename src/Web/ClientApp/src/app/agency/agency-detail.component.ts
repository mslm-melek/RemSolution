import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
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
import { ImpersonationService } from '../shared/impersonation.service';
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
  usersSource = new MatTableDataSource<AgencyUserDto>([]);
  usersColumns: string[] = ['userName', 'role', 'status', 'permissions', 'actions'];

  subscriptions: AgencySubscriptionDto[] = [];
  subscriptionsSource = new MatTableDataSource<AgencySubscriptionDto>([]);

  // Two sortable tables in one component, each behind its own tab, so each sort
  // header is taken through a setter rather than a single ngAfterViewInit.
  @ViewChild('usersSort') set usersSort(sort: MatSort | undefined) {
    if (!sort) return;
    this.usersSource.sortingDataAccessor = (user, column) => {
      switch (column) {
        case 'role': return user.role ?? '';
        case 'status': return user.isLockedOut ? 1 : 0;
        case 'permissions': return (user.permissions ?? []).length;
        default: return user.userName ?? '';
      }
    };
    this.usersSource.sort = sort;
  }

  @ViewChild('subsSort') set subsSort(sort: MatSort | undefined) {
    if (!sort) return;
    this.subscriptionsSource.sortingDataAccessor = (subscription, column) => {
      switch (column) {
        case 'status': return subscription.status ?? 0;
        // The period column shows both bounds; it sorts by the start.
        case 'period': return subscription.startDate ? new Date(subscription.startDate).getTime() : 0;
        default: return subscription.planName ?? '';
      }
    };
    this.subscriptionsSource.sort = sort;
  }

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
    private plansClient: SubscriptionPlansClient,
    private impersonation: ImpersonationService
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

  // Opens this agency's workspace: from here on every module screen reads and
  // writes this agency's data, until the banner's exit. Reloads the page — the
  // signed-in user's permissions and enabled features come from one fetch per app
  // load and both change with the agency.
  openWorkspace(landOn = '/dashboard') {
    if (!this.agency) return;

    this.impersonation.enter(
      { id: this.agencyId, name: this.agency.name || this.transloco.translate('agency.fallbackName') },
      landOn);
  }

  // --- Users ---
  loadUsers() {
    this.usersClient.getAgencyUsers(this.agencyId).subscribe({
      next: u => {
        this.users = u || [];
        this.usersSource.data = this.users;
      },
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
      next: s => {
        this.subscriptions = s || [];
        this.subscriptionsSource.data = this.subscriptions;
      },
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
