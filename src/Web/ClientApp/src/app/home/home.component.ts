import { Component, OnInit } from '@angular/core';
import { AuthService } from '../shared/auth.service';
import { AgenciesClient, BrandsClient, CarsClient, ClientsClient, ModelCarsClient, SubscriptionPlansClient } from '../web-api-client';

interface StatTile {
  // Transloco key; resolved in the template so a language switch re-renders it.
  labelKey: string;
  value: number | null;
  icon: string;
  link: string;
}

interface QuickAction {
  labelKey: string;
  icon: string;
  link: string;
}

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit {
  isAuthenticated: boolean | null = null;
  isPlatformAdmin = false;
  isCustomer = false;
  displayName: string | null | undefined;

  // Agency-user dashboard (tenant-scoped module counts).
  agencyStats: StatTile[] = [
    { labelKey: 'home.stats.cars', value: null, icon: 'directions_car', link: '/car' },
    { labelKey: 'home.stats.clients', value: null, icon: 'group', link: '/client' },
    { labelKey: 'home.stats.carModels', value: null, icon: 'category', link: '/model-car' },
    { labelKey: 'home.stats.brands', value: null, icon: 'sell', link: '/brand' }
  ];

  agencyQuickActions: QuickAction[] = [
    { labelKey: 'home.quick.newCar', icon: 'add', link: '/car/new' },
    { labelKey: 'home.quick.newClient', icon: 'person_add', link: '/client/new' },
    { labelKey: 'home.quick.newModel', icon: 'playlist_add', link: '/model-car/new' }
  ];

  // Platform-admin dashboard (cross-tenant catalog counts).
  adminStats: StatTile[] = [
    { labelKey: 'home.stats.agencies', value: null, icon: 'business', link: '/agency' },
    { labelKey: 'home.stats.subscriptionPlans', value: null, icon: 'workspace_premium', link: '/subscription-plan' },
    { labelKey: 'home.stats.carModels', value: null, icon: 'category', link: '/model-car' },
    { labelKey: 'home.stats.brands', value: null, icon: 'sell', link: '/brand' }
  ];

  adminQuickActions: QuickAction[] = [
    { labelKey: 'home.quick.newAgency', icon: 'add_business', link: '/agency/new' },
    { labelKey: 'home.quick.newPlan', icon: 'add', link: '/subscription-plan/new' }
  ];

  constructor(
    private auth: AuthService,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private modelCarsClient: ModelCarsClient,
    private brandsClient: BrandsClient,
    private agenciesClient: AgenciesClient,
    private plansClient: SubscriptionPlansClient
  ) { }

  get stats(): StatTile[] {
    return this.isPlatformAdmin ? this.adminStats : this.agencyStats;
  }

  get quickActions(): QuickAction[] {
    return this.isPlatformAdmin ? this.adminQuickActions : this.agencyQuickActions;
  }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user);
      this.isCustomer = AuthService.isCustomer(user);
      this.displayName = user.fullName || user.userName;

      // Customers get a browse-oriented home, not the staff dashboard (and none
      // of the staff stat calls, which they aren't authorized for).
      if (!this.isAuthenticated || this.isCustomer) {
        return;
      }

      if (this.isPlatformAdmin) {
        this.loadAdminStats();
      } else {
        this.loadAgencyStats();
      }
    });
  }

  private loadAgencyStats() {
    // Page size 1: only totalCount is needed for the tiles.
    this.carsClient.getCars(1, 1, null, null, null).subscribe({
      next: r => this.agencyStats[0].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.clientsClient.getClients(1, 1, null, null).subscribe({
      next: r => this.agencyStats[1].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.modelCarsClient.getModelCars(1, 1, null).subscribe({
      next: r => this.agencyStats[2].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.brandsClient.getBrands().subscribe({
      next: r => this.agencyStats[3].value = (r || []).length,
      error: err => console.error(err)
    });
  }

  private loadAdminStats() {
    this.agenciesClient.getAgencies().subscribe({
      next: r => this.adminStats[0].value = (r || []).length,
      error: err => console.error(err)
    });
    this.plansClient.getSubscriptionPlans().subscribe({
      next: r => this.adminStats[1].value = (r || []).length,
      error: err => console.error(err)
    });
    this.modelCarsClient.getModelCars(1, 1, null).subscribe({
      next: r => this.adminStats[2].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.brandsClient.getBrands().subscribe({
      next: r => this.adminStats[3].value = (r || []).length,
      error: err => console.error(err)
    });
  }
}
