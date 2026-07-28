import { Component, OnInit } from '@angular/core';
import { AuthService } from '../shared/auth.service';
import { LanguageService } from '../shared/language.service';
import { AppLanguage } from '../shared/language';

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
  canAccessCredits = false;
  canAccessDashboard = false;
  canAccessChat = false;
  // Paperwork layouts live under Config; either document module getting them there
  // is enough, since one screen manages both kinds.
  canAccessDocumentTemplates = false;
  // The Config dropdown shows when at least one config/reference item is reachable.
  canAccessConfig = false;

  readonly languages: AppLanguage[];

  constructor(private auth: AuthService, private language: LanguageService) {
    this.languages = this.language.available;
  }

  get currentLanguage(): AppLanguage {
    return this.language.current;
  }

  // Persists the choice and reloads — see LanguageService.use for why.
  setLanguage(language: AppLanguage) {
    this.collapse();
    this.language.use(language);
  }

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
      this.canAccessCredits = AuthService.canAccessModule(user, 'Credits', 'Credit.Read');
      this.canAccessDashboard = AuthService.canAccessModule(user, 'Dashboard', 'Dashboard.View');
      this.canAccessChat = AuthService.canAccessModule(user, 'Chat', 'Chat.View');
      // Config holds administrator-only, feature-gated reference screens (type
      // catalogs, car brands/models). Team moved to the user menu. Show the
      // dropdown only when the admin actually has one of those features.
      this.canAccessDocumentTemplates =
        AuthService.canAccessModule(user, 'Contracts', 'Contract.Read')
        || AuthService.canAccessModule(user, 'Factures', 'Facture.Read');

      this.canAccessConfig = this.isAgencyAdmin
        && (this.canAccessCars || this.canAccessExtraServices || this.canAccessExpenses
            || this.canAccessDocumentTemplates);
    });
  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
