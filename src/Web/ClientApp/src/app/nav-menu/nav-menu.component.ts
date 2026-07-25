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
  // Agency administrator can manage their own agency's staff (Team screen).
  isAgencyAdmin = false;
  // Self-registered marketplace customer: browse/book, not the staff nav.
  isCustomer = false;
  // Feature off for the agency, or read permission missing ⇒ module hidden.
  canAccessCars = false;
  canAccessClients = false;
  canAccessRentings = false;
  canAccessReservations = false;
  canAccessExtraServices = false;
  canAccessExpenses = false;
  // The Config dropdown shows when at least one config/reference item is reachable.
  canAccessConfig = false;

  constructor(private auth: AuthService) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.displayName = user.fullName || user.userName;
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user);
      this.isAgencyAdmin = user.role === 'AgencyAdministrator';
      this.isCustomer = AuthService.isCustomer(user);
      this.canAccessCars = AuthService.canAccessModule(user, 'Cars', 'Car.Read');
      this.canAccessClients = AuthService.canAccessModule(user, 'Clients', 'Client.Read');
      this.canAccessRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canAccessReservations = AuthService.canAccessModule(user, 'Reservations', 'Reservation.Read');
      this.canAccessExtraServices = AuthService.canAccessModule(user, 'ExtraServices', 'ExtraService.Read');
      this.canAccessExpenses = AuthService.canAccessModule(user, 'Expenses', 'Expense.Read');
      // Config holds administrator-only, feature-gated reference screens (type
      // catalogs, car brands/models). Team moved to the user menu. Show the
      // dropdown only when the admin actually has one of those features.
      this.canAccessConfig = this.isAgencyAdmin
        && (this.canAccessCars || this.canAccessExtraServices || this.canAccessExpenses);
    });
  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
