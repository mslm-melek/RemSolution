import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../shared/auth.service';
import { BranchScopeService } from '../shared/branch-scope.service';
import { ImpersonationService, ImpersonatedAgency } from '../shared/impersonation.service';
import { LanguageService } from '../shared/language.service';
import { AppLanguage } from '../shared/language';
import { NotificationBadgeService } from '../shared/notification-badge.service';
import { ThemeService } from '../shared/theme.service';
import { AppTheme } from '../shared/theme';
import { TodayBranchDto } from '../web-api-client';

/** Where the app bar's one search box can send a term. */
interface SearchTarget {
  key: string;
  /** Transloco key under `search.*`. */
  labelKey: string;
  icon: string;
  link: string;
}

/**
 * The bar across the top of every screen: where you are standing, what you are
 * looking for, and the three switches that belong to the app rather than to any
 * page (alerts, theme, language).
 *
 * Signed out it keeps only the brand and the way in — the marketplace is a public
 * website and has no rail beside it.
 */
@Component({
  selector: 'app-top-bar',
  templateUrl: './top-bar.component.html',
  styleUrls: ['./top-bar.component.scss']
})
export class TopBarComponent implements OnInit {
  /** The rail's overlay toggle, on a narrow screen. */
  @Output() menu = new EventEmitter<void>();

  isAuthenticated = false;
  isCustomer = false;

  // Set while a platform admin has an agency's workspace open. The banner is
  // deliberately loud and always present: from here on every screen is that
  // agency's, and what the admin does lands in its records under their own name.
  workspace: ImpersonatedAgency | null = null;

  // The bell. Shown on the agency's own feature alone — reading one's own inbox
  // needs no permission — and never to a customer, who has no staff alerts.
  canNotifications = false;
  unreadCount = 0;

  // --- Branch picker --------------------------------------------------------
  // Drawn only where the scope actually applies (see BranchScopeService): the
  // home screen counts its figures for one branch, nothing else does yet.
  branches: TodayBranchDto[] = [];
  branchId: number | null = null;
  private onHome = true;

  // --- Search ---------------------------------------------------------------
  searchTargets: SearchTarget[] = [];
  searchTarget: SearchTarget | null = null;
  searchTerm = '';

  readonly languages: AppLanguage[];

  constructor(
    private auth: AuthService,
    private impersonation: ImpersonationService,
    private language: LanguageService,
    private theme: ThemeService,
    private badge: NotificationBadgeService,
    private branchScope: BranchScopeService,
    private router: Router
  ) {
    this.languages = this.language.available;
    this.workspace = this.impersonation.current;
  }

  ngOnInit() {
    this.badge.unreadCount$.subscribe(count => this.unreadCount = count);

    this.onHome = isHome(this.router.url);
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => this.onHome = isHome(event.urlAfterRedirects));

    this.branchScope.branches$.subscribe(branches => this.branches = branches);
    this.branchScope.branchId$.subscribe(id => this.branchId = id);

    this.auth.currentUser$.subscribe(user => {
      this.isAuthenticated = user.isAuthenticated ?? false;
      this.isCustomer = AuthService.isCustomer(user);

      if (this.workspace && user.isImpersonating && user.agencyName) {
        this.workspace = { ...this.workspace, name: user.agencyName };
      }

      this.canNotifications =
        !this.isCustomer && (user.features?.includes('Notifications') ?? false);

      // Only the lists this user can actually open. Each one reads `?search=`
      // from the URL, so the bar hands the term over by linking to it — there is
      // no separate search endpoint, and no result screen that could disagree
      // with the list it came from.
      const can = (feature: string, permission: string) =>
        AuthService.canAccessModule(user, feature, permission);

      this.searchTargets = [
        can('Cars', 'Car.Read')
          ? { key: 'cars', labelKey: 'search.cars', icon: 'directions_car', link: '/car' } : null,
        can('Clients', 'Client.Read')
          ? { key: 'clients', labelKey: 'search.clients', icon: 'group', link: '/client' } : null,
        can('Rentings', 'Renting.Read')
          ? { key: 'bookings', labelKey: 'search.bookings', icon: 'vpn_key', link: '/renting' } : null
      ].filter((target): target is SearchTarget => target !== null);

      this.searchTarget = this.searchTargets[0] ?? null;
    });
  }

  // --- Branch ---------------------------------------------------------------

  /** The picker is only meaningful where the scope is applied, and only when
   *  there is more than one answer. */
  get showBranchPicker(): boolean {
    return this.onHome && this.branches.length > 0;
  }

  get branchLabel(): string | null {
    return this.branchScope.nameOf(this.branchId);
  }

  selectBranch(branchId: number | null) {
    this.branchScope.select(branchId);
  }

  // --- Search ---------------------------------------------------------------

  get showSearch(): boolean {
    return this.isAuthenticated && !this.isCustomer && this.searchTargets.length > 0;
  }

  pickTarget(target: SearchTarget) {
    this.searchTarget = target;
    if (this.searchTerm.trim()) this.submitSearch();
  }

  submitSearch() {
    const term = this.searchTerm.trim();
    if (!term || !this.searchTarget) return;

    // Replaces the list's whole query string, exactly as its own search box does
    // (see applyListFilters): arriving from here is arriving with one filter set,
    // not with the previous visit's still on.
    this.router.navigate([this.searchTarget.link], { queryParams: { search: term } });
  }

  clearSearch() {
    this.searchTerm = '';
  }

  // --- App switches ---------------------------------------------------------

  get currentTheme(): AppTheme {
    return this.theme.active;
  }

  // No reload and no menu: one button straight to the other theme. Every colour
  // resolves through a custom property, so the switch is a single attribute.
  toggleTheme() {
    this.theme.toggle();
  }

  get currentLanguage(): AppLanguage {
    return this.language.current;
  }

  // Persists the choice and reloads — see LanguageService.use for why.
  setLanguage(language: AppLanguage) {
    this.language.use(language);
  }

  exitWorkspace() {
    this.impersonation.exit();
  }
}

function isHome(url: string): boolean {
  return url === '/' || url.startsWith('/?');
}
