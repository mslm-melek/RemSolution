import { Component, OnInit } from '@angular/core';
import { AuthService } from '../shared/auth.service';

@Component({
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.scss']
})
export class NavMenuComponent implements OnInit {
  isExpanded = false;
  isAuthenticated = false;
  displayName: string | null | undefined;
  // Platform admin (app owner) sees the agency-grouped console; agency users
  // see the flat module list below.
  isPlatformAdmin = false;
  // Feature off for the agency, or read permission missing ⇒ module hidden.
  canAccessCars = false;
  canAccessClients = false;

  constructor(private auth: AuthService) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.displayName = user.fullName || user.userName;
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user);
      this.canAccessCars = AuthService.canAccessModule(user, 'Cars', 'Car.Read');
      this.canAccessClients = AuthService.canAccessModule(user, 'Clients', 'Client.Read');
    });
  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
