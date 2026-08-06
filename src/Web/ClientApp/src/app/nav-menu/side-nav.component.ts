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
   * Extra paths this entry owns, beyond the one it navigates to. The bookings
   * screen is at /booking but the forms behind it are still at /renting/:id and
   * /reservation/:id, and the rail has to keep saying where the user is while one
   * of those is open.
   */
  alsoAt?: string[];
}

/**
 * The app's navigation rail: the modules this user can reach, then who they are
 * signed in as.
 *
 * A rail rather than a bar because the list is long and grows — five module
 * groups, a configuration menu and an account menu do not fit across the top of
 * a screen that also has to carry a branch picker and a search box. Vertically
 * there is room for every entry to be a labelled, iconned target, and a group
 * opens in place instead of behind a dropdown.
 *
 * What is IN it is unchanged and still data-driven: the role and the agency's
 * features decide, so an entry is never drawn for a screen that would 403.
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

  // Set while a platform admin has an agency's workspace open: the rail becomes
  // that agency's module nav. Read from the client-side session rather than the
  // role, since the role stays PlatformAdministrator throughout.
  workspace: ImpersonatedAgency | null = null;

  navEntries: NavEntry[] = [];

  // Reference/catalog screens. These are administration, not day-to-day work, so
  // they sit in a menu at the foot of the rail instead of taking a slot in it.
  configLinks: NavLink[] = [];

  // A group has no routerLinkActive of its own (its header is a button), so the
  // open one is derived from the URL — and a group whose screen is open starts
  // open, rather than hiding the item the user is looking at.
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

      // The banner renders from the stored session so it is up before this call
      // resolves, but the server is the authority once it does.
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

  /** The initials on the account tile — two letters, however the name is spelled. */
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
    // Inside an agency workspace the platform admin gets that agency's own nav,
    // not the console's — the API answers every request for the agency, and it
    // returns the agency's features and the full permission set, so the same
    // feature-driven rules below produce exactly the screens that will work.
    if (this.isPlatformAdmin && !this.workspace) {
      // No dashboard entry: the console dashboard IS the platform admin's home,
      // so the Home item at the top of the rail already leads there.
      return [
        { labelKey: 'nav.agencies', icon: 'business', link: '/agency' },
        { labelKey: 'nav.subscriptionPlans', icon: 'workspace_premium', link: '/subscription-plan' }
      ];
    }

    if (this.isCustomer) {
      return [
        { labelKey: 'nav.browseCars', icon: 'travel_explore', link: '/browse' },
        { labelKey: 'nav.myReservations', icon: 'event_available', link: '/my-reservations' },
        // Past and current rentals — and where a finished one gets rated.
        { labelKey: 'nav.myRentings', icon: 'vpn_key', link: '/my-rentings' },
        // Not feature-gated: the customer's own threads. An agency without the
        // Chat feature simply never opens one, so the list stays empty.
        { labelKey: 'nav.myChats', icon: 'forum', link: '/my-chats' }
      ];
    }

    // Agency staff: the day's work, grouped by what it is about.
    const entries: (NavEntry | null)[] = [
      // The overview and the statistics report are the same entitlement (see
      // GetStatisticsQuery), so this group either has both screens or neither.
      this.group('nav.dashboard', 'insights', [
        can('Dashboard', 'Dashboard.View')
          ? { labelKey: 'nav.overview', link: '/dashboard', icon: 'space_dashboard' } : null,
        can('Dashboard', 'Dashboard.View')
          ? { labelKey: 'nav.statistics', link: '/statistics', icon: 'insights' } : null
      ]),
      can('Cars', 'Car.Read')
        ? { labelKey: 'nav.cars', icon: 'directions_car', link: '/car' } : null,
      // One screen for both: hires and holds are the same booking at two points
      // of its life, and the merged list tabs between them (see BookingComponent).
      // Either entitlement opens it, and the screen shows only the tab the user
      // can actually read, so the rail does not have to say which.
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
      // One finance screen: the credits page carries both directions of money and
      // absorbed the expense list. Either entitlement opens it, and the label
      // says which half the user will actually find there.
      this.group('nav.finance', 'request_quote', [
        can('Credits', 'Credit.Read')
          ? { labelKey: 'nav.credits', link: '/credit', icon: 'request_quote' }
          : can('Expenses', 'Expense.Read')
            ? { labelKey: 'nav.expenses', link: '/credit', icon: 'payments' } : null
      ])
    ];

    return entries.filter((entry): entry is NavEntry => entry !== null);
  }

  // A group with nothing in it disappears; a group with a single reachable
  // screen becomes a plain item, so nobody expands a list to pick the only entry.
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

  /** Whether a group's sublist is showing. Open by default while it holds the
   *  current screen, so the rail never hides where the user already is. */
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

  /**
   * Whether a plain item is the screen on show. Worked out here rather than left
   * to routerLinkActive, because an entry can own more than the one path it
   * navigates to (see {@link NavEntry.alsoAt}).
   */
  isItemActive(entry: NavEntry): boolean {
    return [entry.link, ...(entry.alsoAt ?? [])]
      .some((path): path is string => !!path && this.isAt(path));
  }

  /** On that path, or on something under it. The query string is not part of the
   *  answer: a filtered list is still the same screen. */
  private isAt(path: string): boolean {
    const url = this.currentUrl.split(/[?#]/)[0];
    return url === path || url.startsWith(path + '/');
  }

  exitWorkspace() {
    this.impersonation.exit();
  }

  onNavigate() {
    this.navigated.emit();
  }
}
