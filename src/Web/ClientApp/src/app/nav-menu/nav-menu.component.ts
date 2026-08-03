import { Component, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../shared/auth.service';
import { ImpersonationService, ImpersonatedAgency } from '../shared/impersonation.service';
import { LanguageService } from '../shared/language.service';
import { AppLanguage } from '../shared/language';

// One screen reachable from the bar or from a dropdown.
export interface NavLink {
  labelKey: string;
  link: string;
  icon: string;
}

// A slot in the top bar. `link` set ⇒ it is a direct link; `links` set ⇒ it is a
// dropdown. Never both (see NavMenuComponent.group).
export interface NavEntry {
  labelKey: string;
  link?: string;
  links?: NavLink[];
}

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
  // see the module groups below.
  isPlatformAdmin = false;
  // Agency administrator can manage their own agency's staff (Team screen).
  isAgencyAdmin = false;
  // Self-registered marketplace customer: browse/book, not the staff nav.
  isCustomer = false;
  // Set while a platform admin has an agency's workspace open: the bar becomes
  // that agency's module nav and the banner below the toolbar says whose data is
  // on screen. Read from the client-side session rather than the role, since the
  // role stays PlatformAdministrator throughout.
  workspace: ImpersonatedAgency | null = null;

  // The bar is built from the user's role and entitlements rather than spelled
  // out in the template: the grouping rules (below) then live in one place.
  navEntries: NavEntry[] = [];

  // Reference/catalog screens. These are administration, not day-to-day work, so
  // they hang off the user menu instead of taking a slot in the bar.
  configLinks: NavLink[] = [];

  // Group dropdowns cannot use routerLinkActive (the trigger is a button, not a
  // link), so the active group is derived from the current URL.
  private currentUrl = '/';

  readonly languages: AppLanguage[];

  constructor(
    private auth: AuthService,
    private impersonation: ImpersonationService,
    private language: LanguageService,
    private router: Router
  ) {
    this.languages = this.language.available;
    this.workspace = this.impersonation.current;
  }

  exitWorkspace() {
    this.collapse();
    this.impersonation.exit();
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
    this.currentUrl = this.router.url;
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => this.currentUrl = event.urlAfterRedirects);

    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.displayName = user.fullName || user.userName;
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user);
      this.isAgencyAdmin = user.role === 'AgencyAdministrator';
      this.isCustomer = AuthService.isCustomer(user);

      // The banner renders from the stored session so it is up before this call
      // resolves, but the server is the authority once it does: it confirms the
      // request really was impersonated, and its agency name is current where a
      // stored one goes stale after a rename.
      if (this.workspace && user.isImpersonating && user.agencyName) {
        this.workspace = { ...this.workspace, name: user.agencyName };
      }

      // Feature off for the agency, or read permission missing ⇒ screen hidden.
      const can = (feature: string, permission: string) =>
        AuthService.canAccessModule(user, feature, permission);

      // Paperwork layouts: either document module getting them there is enough,
      // since one screen manages both kinds.
      const canDocumentTemplates =
        can('Contracts', 'Contract.Read') || can('Factures', 'Facture.Read');

      this.navEntries = this.buildEntries(can);
      this.configLinks = this.buildConfigLinks(can, canDocumentTemplates);
    });
  }

  private buildEntries(can: (feature: string, permission: string) => boolean): NavEntry[] {
    // Inside an agency workspace the platform admin gets that agency's own nav,
    // not the console's — the API answers every request for the agency, and it
    // returns the agency's features and the full permission set, so the same
    // feature-driven rules below produce exactly the screens that will work.
    if (this.isPlatformAdmin && !this.workspace) {
      // No dashboard entry: the console dashboard IS the platform admin's home,
      // so the Home link above already leads there.
      return [
        { labelKey: 'nav.agencies', link: '/agency' },
        { labelKey: 'nav.subscriptionPlans', link: '/subscription-plan' }
      ];
    }

    if (this.isCustomer) {
      return [
        { labelKey: 'nav.browseCars', link: '/browse' },
        { labelKey: 'nav.myReservations', link: '/my-reservations' },
        // Past and current rentals — and where a finished one gets rated.
        { labelKey: 'nav.myRentings', link: '/my-rentings' },
        // Not feature-gated: the customer's own threads. An agency without the
        // Chat feature simply never opens one, so the list stays empty.
        { labelKey: 'nav.myChats', link: '/my-chats' }
      ];
    }

    // Agency staff: the day's work, grouped by what it is about.
    const entries: (NavEntry | null)[] = [
      can('Dashboard', 'Dashboard.View') ? { labelKey: 'nav.dashboard', link: '/dashboard' } : null,
      can('Cars', 'Car.Read') ? { labelKey: 'nav.cars', link: '/car' } : null,
      this.group('nav.bookings', [
        can('Reservations', 'Reservation.Read')
          ? { labelKey: 'nav.reservations', link: '/reservation', icon: 'event_available' } : null,
        can('Rentings', 'Renting.Read')
          ? { labelKey: 'nav.rentings', link: '/renting', icon: 'vpn_key' } : null
      ]),
      this.group('nav.clients', [
        can('Clients', 'Client.Read')
          ? { labelKey: 'nav.clientsList', link: '/client', icon: 'group' } : null,
        can('Chat', 'Chat.View')
          ? { labelKey: 'nav.chat', link: '/chat', icon: 'forum' } : null
      ]),
      // One finance screen: the credits page carries both directions of money and
      // absorbed the expense list. Either entitlement opens it, and the label
      // says which half the user will actually find there.
      this.group('nav.finance', [
        can('Credits', 'Credit.Read')
          ? { labelKey: 'nav.credits', link: '/credit', icon: 'request_quote' }
          : can('Expenses', 'Expense.Read')
            ? { labelKey: 'nav.expenses', link: '/credit', icon: 'payments' } : null
      ])
    ];

    return entries.filter((entry): entry is NavEntry => entry !== null);
  }

  // A group with nothing in it disappears; a group with a single reachable
  // screen becomes a plain link, so nobody opens a menu to pick the only item.
  private group(labelKey: string, links: (NavLink | null)[]): NavEntry | null {
    const reachable = links.filter((link): link is NavLink => link !== null);

    if (!reachable.length) return null;
    if (reachable.length === 1) return { labelKey: reachable[0].labelKey, link: reachable[0].link };

    return { labelKey, links: reachable };
  }

  private buildConfigLinks(
    can: (feature: string, permission: string) => boolean,
    canDocumentTemplates: boolean
  ): NavLink[] {
    if (this.isCustomer) return [];

    if (this.isPlatformAdmin && !this.workspace) {
      return [
        { labelKey: 'nav.carBrands', link: '/brand', icon: 'sell' },
        { labelKey: 'nav.carModels', link: '/model-car', icon: 'category' },
        { labelKey: 'nav.extraServiceTypes', link: '/extra-service-type', icon: 'add_shopping_cart' },
        { labelKey: 'nav.expenseTypes', link: '/expense-type', icon: 'receipt_long' }
      ];
    }

    // Reference data is administrator-only, and each screen stays gated by the
    // feature it belongs to. A platform admin in an agency workspace counts as
    // that agency's administrator here: the catalogue screens accept either
    // administrator role, so every link below is one they can actually open.
    if (!this.isAgencyAdmin && !this.workspace) return [];

    const links: (NavLink | null)[] = [
      can('Expenses', 'Expense.Read')
        ? { labelKey: 'nav.expenseTypes', link: '/expense-type', icon: 'receipt_long' } : null,
      can('ExtraServices', 'ExtraService.Read')
        ? { labelKey: 'nav.extraServiceTypes', link: '/extra-service-type', icon: 'add_shopping_cart' } : null,
      can('Cars', 'Car.Read')
        ? { labelKey: 'nav.carBrands', link: '/brand', icon: 'sell' } : null,
      can('Cars', 'Car.Read')
        ? { labelKey: 'nav.carModels', link: '/model-car', icon: 'category' } : null,
      canDocumentTemplates
        ? { labelKey: 'nav.documentTemplates', link: '/document-template', icon: 'description' } : null
    ];

    return links.filter((link): link is NavLink => link !== null);
  }

  // Highlights a dropdown while one of its screens is open.
  isGroupActive(entry: NavEntry): boolean {
    return (entry.links ?? []).some(child =>
      this.currentUrl === child.link || this.currentUrl.startsWith(child.link + '/'));
  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
