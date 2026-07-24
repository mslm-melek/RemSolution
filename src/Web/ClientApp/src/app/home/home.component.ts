import { Component, OnInit } from '@angular/core';
import { AuthService } from '../shared/auth.service';
import { AgenciesClient, BrandsClient, CarsClient, ClientsClient, ModelCarsClient, SubscriptionPlansClient } from '../web-api-client';

interface StatTile {
  label: string;
  value: number | null;
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
  displayName: string | null | undefined;

  // Agency-user dashboard (tenant-scoped module counts).
  agencyStats: StatTile[] = [
    { label: 'Cars', value: null, icon: 'directions_car', link: '/car' },
    { label: 'Clients', value: null, icon: 'group', link: '/client' },
    { label: 'Car Models', value: null, icon: 'category', link: '/model-car' },
    { label: 'Brands', value: null, icon: 'sell', link: '/brand' }
  ];

  agencyQuickActions = [
    { label: 'New Car', icon: 'add', link: '/car/new' },
    { label: 'New Client', icon: 'person_add', link: '/client/new' },
    { label: 'New Model', icon: 'playlist_add', link: '/model-car/new' }
  ];

  // Platform-admin dashboard (cross-tenant catalog counts).
  adminStats: StatTile[] = [
    { label: 'Agencies', value: null, icon: 'business', link: '/agency' },
    { label: 'Subscription Plans', value: null, icon: 'workspace_premium', link: '/subscription-plan' },
    { label: 'Car Models', value: null, icon: 'category', link: '/model-car' },
    { label: 'Brands', value: null, icon: 'sell', link: '/brand' }
  ];

  adminQuickActions = [
    { label: 'New Agency', icon: 'add_business', link: '/agency/new' },
    { label: 'New Plan', icon: 'add', link: '/subscription-plan/new' }
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

  get quickActions() {
    return this.isPlatformAdmin ? this.adminQuickActions : this.agencyQuickActions;
  }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user);
      this.displayName = user.fullName || user.userName;

      if (!this.isAuthenticated) {
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
