import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription, timer } from 'rxjs';
import { AuthService } from '../shared/auth.service';
import { ImpersonationService } from '../shared/impersonation.service';
import {
  AgenciesClient, BrandsClient, CarsClient, ClientsClient, MarketplaceCarDto,
  MarketplaceClient, ModelCarsClient, SubscriptionPlansClient
} from '../web-api-client';

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

// How long each car stays on screen in the home-page slideshow.
const SLIDE_INTERVAL_MS = 6_000;
// Slides in the shop window. More than this and nobody reaches the end.
const SHOWCASE_SIZE = 8;

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {
  isAuthenticated: boolean | null = null;
  isPlatformAdmin = false;
  isCustomer = false;
  displayName: string | null | undefined;

  // Shop-window slideshow, shown to visitors and customers (staff get the
  // dashboard instead). Cars come from the public marketplace, so an anonymous
  // visitor can see them before signing in.
  showcase: MarketplaceCarDto[] = [];
  slide = 0;
  private autoplay?: Subscription;
  private showcaseRequested = false;

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
    private impersonation: ImpersonationService,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private modelCarsClient: ModelCarsClient,
    private brandsClient: BrandsClient,
    private agenciesClient: AgenciesClient,
    private plansClient: SubscriptionPlansClient,
    private marketplaceClient: MarketplaceClient
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
      // A platform admin inside an agency workspace is looking at that agency, so
      // the tenant-scoped tiles and quick actions are the right ones — the console
      // counts (agencies, plans) belong to the screens outside the workspace.
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user) && !this.impersonation.current;
      this.isCustomer = AuthService.isCustomer(user);
      this.displayName = user.fullName || user.userName;

      // Customers get a browse-oriented home, not the staff dashboard (and none
      // of the staff stat calls, which they aren't authorized for).
      if (!this.isAuthenticated || this.isCustomer) {
        this.loadShowcase();
        return;
      }

      if (this.isPlatformAdmin) {
        this.loadAdminStats();
      } else {
        this.loadAgencyStats();
      }
    });
  }

  ngOnDestroy() {
    this.autoplay?.unsubscribe();
  }

  // Advancing on a click also stops the timer: a card must not slide away from
  // under someone who has taken control of the slideshow.
  prevSlide() {
    this.autoplay?.unsubscribe();
    this.slide = (this.slide - 1 + this.showcase.length) % this.showcase.length;
  }

  nextSlide() {
    this.autoplay?.unsubscribe();
    this.slide = (this.slide + 1) % this.showcase.length;
  }

  goToSlide(index: number) {
    this.autoplay?.unsubscribe();
    this.slide = index;
  }

  private loadShowcase() {
    // currentUser$ can emit more than once; the slideshow is loaded once.
    if (this.showcaseRequested) {
      return;
    }
    this.showcaseRequested = true;

    this.marketplaceClient.getShowcaseCars(SHOWCASE_SIZE).subscribe({
      next: cars => {
        this.showcase = cars || [];
        if (this.showcase.length > 1) {
          this.autoplay = timer(SLIDE_INTERVAL_MS, SLIDE_INTERVAL_MS)
            .subscribe(() => this.slide = (this.slide + 1) % this.showcase.length);
        }
      },
      // An empty shop window is not worth an error banner on the landing page.
      error: err => console.error(err)
    });
  }

  private loadAgencyStats() {
    // Page size 1: only totalCount is needed for the tiles.
    this.carsClient.getCars(1, 1, null, null, null, null, false).subscribe({
      next: r => this.agencyStats[0].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.clientsClient.getClients(1, 1, null, null, null, false).subscribe({
      next: r => this.agencyStats[1].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.modelCarsClient.getModelCars(1, 1, null, null, false).subscribe({
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
    this.modelCarsClient.getModelCars(1, 1, null, null, false).subscribe({
      next: r => this.adminStats[2].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.brandsClient.getBrands().subscribe({
      next: r => this.adminStats[3].value = (r || []).length,
      error: err => console.error(err)
    });
  }
}
