import { Component, OnInit } from '@angular/core';
import { AuthService } from '../shared/auth.service';
import { BrandsClient, CarsClient, ClientsClient, ModelCarsClient } from '../web-api-client';

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
  displayName: string | null | undefined;

  stats: StatTile[] = [
    { label: 'Cars', value: null, icon: 'directions_car', link: '/car' },
    { label: 'Clients', value: null, icon: 'group', link: '/client' },
    { label: 'Car Models', value: null, icon: 'category', link: '/model-car' },
    { label: 'Brands', value: null, icon: 'sell', link: '/brand' }
  ];

  quickActions = [
    { label: 'New Car', icon: 'add', link: '/car/new' },
    { label: 'New Client', icon: 'person_add', link: '/client/new' },
    { label: 'New Model', icon: 'playlist_add', link: '/model-car/new' }
  ];

  constructor(
    private auth: AuthService,
    private carsClient: CarsClient,
    private clientsClient: ClientsClient,
    private modelCarsClient: ModelCarsClient,
    private brandsClient: BrandsClient
  ) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.displayName = user.fullName || user.userName;

      if (this.isAuthenticated) {
        this.loadStats();
      }
    });
  }

  private loadStats() {
    // Page size 1: only totalCount is needed for the tiles.
    this.carsClient.getCars(1, 1, null, null, null).subscribe({
      next: r => this.stats[0].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.clientsClient.getClients(1, 1, null, null).subscribe({
      next: r => this.stats[1].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.modelCarsClient.getModelCars(1, 1, null).subscribe({
      next: r => this.stats[2].value = r.totalCount ?? 0,
      error: err => console.error(err)
    });
    this.brandsClient.getBrands().subscribe({
      next: r => this.stats[3].value = (r || []).length,
      error: err => console.error(err)
    });
  }
}
