import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../shared/auth.service';
import { ImpersonationService, ImpersonatedAgency } from '../shared/impersonation.service';

// One screen reachable from the rail, or from a group under it.
export interface NavLink {
  labelKey: string;
  link: string;
  icon: string;
}

// A slot in the rail. `link` set ⇒ it is a direct item; `links` set ⇒ it opens a
// sublist underneath. Never both (see SideNavComponent.group).
export interface NavEntry {
  labelKey: string;
  icon: string;
  link?: string;
  links?: NavLink[];
  /**
   * Extra paths this entry owns, so the rail stays lit on them — /booking also
   * covers /renting/:id and /reservation/:id.
   */
  alsoAt?: string[];
}

/**
 * The navigation rail: the modules this user can reach, then who they are signed
 * in as. Data-driven from the role and the agency's features, so an entry is
 * never drawn for a screen that would 403.
 */
@Component({
  selector: 'app-side-nav',
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss']
})
export class SideNavComponent implements OnInit {
  /** Open as an overlay on a narrow screen; always open on a wide one. */
  @Input() open = false;
  /** A navigation happened — the shell closes the overlay. */
  @Output() navigated = new EventEmitter<void>();

  displayName: string | null | undefined;
  agencyName: string | null | undefined;
  role: string | null | undefined;

  isPlatformAdmin = false;
  isAgencyAdmin = false;
  isCustomer = false;

  // Set while a platform admin has an agency's workspace open; read from the
  // session, since the role stays PlatformAdministrator throughout.
  workspace: ImpersonatedAgency | null = null;

  navEntries: NavEntry[] = [];

  // Reference/catalog screens, in a menu at the foot of the rail.
  configLinks: NavLink[] = [];

  // A group header is a button with no routerLinkActive, so the open one is
  // derived from the URL.
  private currentUrl = '/';
  private toggled = new Set<string>();

  constructor(
    private auth: AuthService,
    private impersonation: ImpersonationService,
    private router: Router
  ) {
    this.workspace = this.impersonation.current;
  }

  ngOnInit() {
    this.currentUrl = this.router.url;
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => this.currentUrl = event.urlAfterRedirects);

    this.auth.currentUser$.subscribe(user => {
      this.displayName = user.fullName || user.userName;
      this.agencyName = user.agencyName;
      this.role = user.role;
      this.isPlatformAdmin = AuthService.isPlatformAdmin(user);
      this.isAgencyAdmin = user.role === 'AgencyAdministrator';
      this.isCustomer = AuthService.isCustomer(user);

      // The banner renders from the session first; the server is authority once here.
      if (this.workspace && user.isImpersonating && user.agencyName) {
        this.workspace = { ...this.workspace, name: user.agencyName };
      }

      // Feature off for the agency, or read permission missing ⇒ screen hidden.
      const can = (feature: string, permission: string) =>
        AuthService.canAccessModule(user, feature, permission);

      // One screen manages both kinds, so either module opens it.
      const canDocumentTemplates =
        can('Contracts', 'Contract.Read') || can('Factures', 'Facture.Read');

      this.navEntries = this.buildEntries(can);
      this.configLinks = this.buildConfigLinks(can, canDocumentTemplates);
    });
  }

  /** The initials on the account tile: up to two letters. */
  get initials(): string {
    const parts = (this.displayName ?? '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  get agencyInitial(): string {
    return (this.agencyName ?? '').trim().charAt(0).toUpperCase() || 'A';
  }

  /** Transloco key for the signed-in person's role, under `roles.*`. */
  get roleLabelKey(): string {
    switch (this.role) {
      case 'PlatformAdministrator': return 'roles.platformAdmin';
      case 'AgencyAdministrator': return 'roles.agencyAdmin';
      case 'Customer': return 'roles.customer';
      default: return 'roles.agencyStaff';
    }
  }

  private buildEntries(can: (feature: string, permission: string) => boolean): NavEntry[] {
    // Inside an agency workspace the platform admin gets that agency's own nav:
    // the API answers with the agency's features and permissions.
    if (this.isPlatformAdmin && !this.workspace) {
      // No dashboard entry: the Home item already leads to the console dashboard.
      return [
        { labelKey: 'nav.agencies', icon: 'business', link: '/agency' },
        // Complaints customers raised against an agency; only the platform
        // settles them (see ResolveAgencyReportCommand).
        { labelKey: 'nav.reports', icon: 'gavel', link: '/agency-reports' },
        { labelKey: 'nav.subscriptionPlans', icon: 'workspace_premium', link: '/subscription-plan' },
        // Display rates for the marketplace; they convert nothing that is stored.
        { labelKey: 'nav.exchangeRates', icon: 'currency_exchange', link: '/exchange-rate' }
      ];
    }

    if (this.isCustomer) {
      // No "browse cars" entry: the Home item already leads to the search, the
      // same way the platform admin's Home leads to the console dashboard.
      return [
        // Holds and rentals on one screen, and where a finished rental gets
        // rated. The two old paths redirect onto its tabs, and `alsoAt` keeps
        // the entry lit while the router is still on one of them.
        {
          labelKey: 'nav.myTrips', icon: 'event_available', link: '/my-bookings',
          alsoAt: ['/my-reservations', '/my-rentings']
        },
        // Not feature-gated: the customer's own threads, empty if the agency has none.
        { labelKey: 'nav.myChats', icon: 'forum', link: '/my-chats' }
      ];
    }

    // Agency staff: the day's work, grouped by what it is about.
    const entries: (NavEntry | null)[] = [
      // Same entitlement for both screens (see GetStatisticsQuery).
      this.group('nav.dashboard', 'insights', [
        can('Dashboard', 'Dashboard.View')
          ? { labelKey: 'nav.overview', link: '/dashboard', icon: 'space_dashboard' } : null,
        can('Dashboard', 'Dashboard.View')
          ? { labelKey: 'nav.statistics', link: '/statistics', icon: 'insights' } : null
      ]),
      can('Cars', 'Car.Read')
        ? { labelKey: 'nav.cars', icon: 'directions_car', link: '/car' } : null,
      // One screen for hires and holds (see BookingComponent); either entitlement
      // opens it and the screen shows only the readable tab.
      can('Rentings', 'Renting.Read') || can('Reservations', 'Reservation.Read')
        ? {
          labelKey: 'nav.bookings', icon: 'event_available', link: '/booking',
          alsoAt: ['/renting', '/reservation']
        } : null,
      this.group('nav.clients', 'group', [
        can('Clients', 'Client.Read')
          ? { labelKey: 'nav.clientsList', link: '/client', icon: 'group' } : null,
        can('Chat', 'Chat.View')
          ? { labelKey: 'nav.chat', link: '/chat', icon: 'forum' } : null
      ]),
      // One finance screen for both directions of money; the label says which
      // half the user will find there.
      this.group('nav.finance', 'request_quote', [
        can('Credits', 'Credit.Read')
          ? { labelKey: 'nav.credits', link: '/credit', icon: 'request_quote' }
          : can('Expenses', 'Expense.Read')
            ? { labelKey: 'nav.expenses', link: '/credit', icon: 'payments' } : null
      ])
    ];

    return entries.filter((entry): entry is NavEntry => entry !== null);
  }

  // An empty group disappears; a group of one becomes a plain item.
  private group(labelKey: string, icon: string, links: (NavLink | null)[]): NavEntry | null {
    const reachable = links.filter((link): link is NavLink => link !== null);

    if (!reachable.length) return null;
    if (reachable.length === 1) {
      return { labelKey: reachable[0].labelKey, icon: reachable[0].icon, link: reachable[0].link };
    }

    return { labelKey, icon, links: reachable };
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

    // Administrator-only, each screen still gated by its feature. A platform admin
    // in a workspace counts as that agency's administrator.
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

  /** Open by default while the group holds the current screen. */
  isOpen(entry: NavEntry): boolean {
    const active = this.isGroupActive(entry);
    return this.toggled.has(entry.labelKey) ? !active : active;
  }

  toggleGroup(entry: NavEntry) {
    if (this.toggled.has(entry.labelKey)) {
      this.toggled.delete(entry.labelKey);
    } else {
      this.toggled.add(entry.labelKey);
    }
  }

  isGroupActive(entry: NavEntry): boolean {
    return (entry.links ?? []).some(child => this.isAt(child.link));
  }

  /** Not routerLinkActive, because an entry can own several paths (see alsoAt). */
  isItemActive(entry: NavEntry): boolean {
    return [entry.link, ...(entry.alsoAt ?? [])]
      .some((path): path is string => !!path && this.isAt(path));
  }

  /** On that path or under it; the query string is ignored. */
  private isAt(path: string): boolean {
    const url = this.currentUrl.split(/[?#]/)[0];
    return url === path || url.startsWith(path + '/');
  }

  exitWorkspace() {
    this.impersonation.exit();
  }

  // Sign-out is a plain link, so the tab's workspace would outlive it. Closing it
  // here spares the reload AuthService would otherwise do.
  onSignOut() {
    this.impersonation.discard();
    this.navigated.emit();
  }

  onNavigate() {
    this.navigated.emit();
  }
}
