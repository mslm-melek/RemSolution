import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { TranslocoService } from '@jsverse/transloco';
import {
  PlatformAgencyRowDto,
  PlatformCountryRowDto,
  PlatformDashboardClient,
  PlatformDashboardDto,
  PlatformPlanRowDto,
  SubscriptionStatus
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { ImpersonationService } from '../shared/impersonation.service';

// Which figure the country bars are drawn from. The three are wildly different
// in magnitude (a country has a handful of agencies and thousands of cars), so
// one shared axis would flatten two of them — hence a picker rather than three
// bars side by side.
type CountryMetric = 'agencies' | 'cars' | 'clients';

// A country row with the bar geometry for the selected metric.
interface CountryBar {
  row: PlatformCountryRowDto;
  value: number;
  // Share of the largest country's value, so the leader always fills the track.
  percent: number;
}

// An item on the "needs attention" list. Only non-zero ones render, so a healthy
// platform gets an all-clear instead of six zeroes.
interface Alert {
  labelKey: string;
  hintKey: string;
  icon: string;
  count: number;
  tone: 'danger' | 'warn' | 'info';
}

@Component({
  selector: 'app-platform-dashboard',
  templateUrl: './platform-dashboard.component.html',
  styleUrls: ['./platform-dashboard.component.css']
})
export class PlatformDashboardComponent implements OnInit, AfterViewInit {
  // Error banners are plain strings, so they are translated imperatively rather
  // than through the template pipe.
  private readonly transloco = inject(TranslocoService);

  data?: PlatformDashboardDto;
  loading = true;
  errorMessage = '';

  alerts: Alert[] = [];

  countryMetric: CountryMetric = 'agencies';
  countryBars: CountryBar[] = [];
  readonly countryMetricOptions: { key: CountryMetric; labelKey: string }[] = [
    { key: 'agencies', labelKey: 'platformDashboard.agencies' },
    { key: 'cars', labelKey: 'platformDashboard.cars' },
    { key: 'clients', labelKey: 'platformDashboard.clients' }
  ];

  plans: PlatformPlanRowDto[] = [];

  // The API returns every agency in one call, so the table sorts and filters on
  // the client.
  agenciesSource = new MatTableDataSource<PlatformAgencyRowDto>([]);
  agencyColumns = ['name', 'country', 'plan', 'status', 'cars', 'clients', 'actions'];

  @ViewChild(MatSort) sort!: MatSort;

  // Referenced by the template to pick the status chip's tone.
  readonly SubscriptionStatus = SubscriptionStatus;

  constructor(
    private client: PlatformDashboardClient,
    private impersonation: ImpersonationService
  ) {
    // Columns whose id is not the property name need to say what they sort on.
    this.agenciesSource.sortingDataAccessor = (agency, column) => {
      switch (column) {
        case 'country': return agency.countryName ?? '';
        case 'plan': return agency.planName ?? '';
        // Live first, then by how close the subscription is to running out.
        case 'status': return agency.subscriptionIsActive
          ? (agency.subscriptionEndDate?.getTime() ?? 0)
          : -1;
        case 'cars': return agency.cars ?? 0;
        case 'clients': return agency.clients ?? 0;
        default: return agency.name ?? '';
      }
    };

    this.agenciesSource.filterPredicate = (agency, filter) =>
      `${agency.name ?? ''} ${agency.countryName ?? ''} ${agency.planName ?? ''}`
        .toLowerCase()
        .includes(filter);
  }

  ngOnInit() {
    this.load();
  }

  ngAfterViewInit() {
    this.agenciesSource.sort = this.sort;
  }

  load() {
    this.loading = true;
    this.errorMessage = '';

    // Notice window left at the API default (30 days) — the screen has no
    // control for it yet.
    this.client.getPlatformDashboard(undefined).subscribe({
      next: data => {
        this.data = data;
        this.plans = data.plans ?? [];
        this.agenciesSource.data = data.agencies ?? [];
        this.alerts = this.buildAlerts(data);
        this.countryBars = this.buildCountryBars(data);
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        const validationErrors = extractValidationErrors(err);
        this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
        if (!validationErrors) console.error(err);
      }
    });
  }

  applyFilter(value: string) {
    this.agenciesSource.filter = (value || '').trim().toLowerCase();
  }

  // Opens the agency's workspace, landing on the screen the clicked figure is
  // about. From there every module reads and writes that agency's data until the
  // banner's exit — see ImpersonationService for why this reloads.
  openAgency(row: PlatformAgencyRowDto, landOn = '/dashboard') {
    if (!row.agencyId) return;

    this.impersonation.enter({ id: row.agencyId, name: row.name ?? '' }, landOn);
  }

  selectCountryMetric(metric: CountryMetric) {
    if (metric === this.countryMetric) return;
    this.countryMetric = metric;
    if (this.data) this.countryBars = this.buildCountryBars(this.data);
  }

  // --- Derived figures ------------------------------------------------------

  // Share of agencies that can actually work today (an inactive subscription
  // blocks every tenant write).
  get subscribedShare(): number | null {
    const agencies = this.data?.totalAgencies ?? 0;
    if (agencies <= 0) return null;
    return ((this.data?.activeSubscriptions ?? 0) / agencies) * 100;
  }

  get averageCarsPerAgency(): number | null {
    const agencies = this.data?.totalAgencies ?? 0;
    if (agencies <= 0) return null;
    return (this.data?.totalCars ?? 0) / agencies;
  }

  // Share of client records that carry a portal login. Null with no clients at
  // all: "0% of nobody" says nothing.
  get clientAccountShare(): number | null {
    const clients = this.data?.totalClients ?? 0;
    if (clients <= 0) return null;
    return ((this.data?.totalClientAccounts ?? 0) / clients) * 100;
  }

  get averageRevenuePerActiveAgency(): number | null {
    const active = this.data?.activeSubscriptions ?? 0;
    if (active <= 0) return null;
    return (this.data?.activePlanRevenue ?? 0) / active;
  }

  // Share of an agency's plan ceiling already used. Null when it has no plan:
  // 0/0 is not "0% used", and a quota bar would imply a limit that isn't there.
  quotaPercent(used?: number, max?: number): number | null {
    if (!max || max <= 0) return null;
    return Math.min(100, ((used ?? 0) / max) * 100);
  }

  statusLabelKey(status?: SubscriptionStatus): string {
    switch (status) {
      case SubscriptionStatus.Active: return 'enums.subscriptionStatus.active';
      case SubscriptionStatus.Suspended: return 'enums.subscriptionStatus.suspended';
      case SubscriptionStatus.Expired: return 'enums.subscriptionStatus.expired';
      default: return '';
    }
  }

  // The chip says what the agency's access actually is, not what the status
  // column says: a row flagged Active whose period has run out is already
  // blocked, and reads "lapsed" here.
  statusTone(agency: PlatformAgencyRowDto): string {
    if (!agency.subscriptionStatus) return 'neutral';
    if (agency.subscriptionIsActive) return 'ok';
    return agency.subscriptionStatus === SubscriptionStatus.Active ? 'danger' : 'warn';
  }

  // --- Country breakdown ----------------------------------------------------

  private buildCountryBars(data: PlatformDashboardDto): CountryBar[] {
    const pick = (row: PlatformCountryRowDto) => {
      switch (this.countryMetric) {
        case 'cars': return row.cars ?? 0;
        case 'clients': return row.clients ?? 0;
        default: return row.agencies ?? 0;
      }
    };

    const rows = data.countries ?? [];
    const peak = Math.max(0, ...rows.map(pick));

    return rows
      .map(row => {
        const value = pick(row);
        return { row, value, percent: peak > 0 ? (value / peak) * 100 : 0 };
      })
      .sort((a, b) => b.value - a.value);
  }

  // --- Needs attention ------------------------------------------------------

  private buildAlerts(data: PlatformDashboardDto): Alert[] {
    const all: Alert[] = [
      {
        labelKey: 'platformDashboard.lapsedSubscriptions',
        hintKey: 'platformDashboard.lapsedHint',
        icon: 'running_with_errors', tone: 'danger',
        count: data.lapsedSubscriptions ?? 0
      },
      {
        labelKey: 'platformDashboard.agenciesWithoutSubscription',
        hintKey: 'platformDashboard.noSubscriptionHint',
        icon: 'money_off', tone: 'danger',
        count: data.agenciesWithoutSubscription ?? 0
      },
      {
        labelKey: 'platformDashboard.expiringSoon',
        hintKey: 'platformDashboard.expiringSoonHint',
        icon: 'event_busy', tone: 'warn',
        count: data.subscriptionsExpiringSoon ?? 0
      },
      {
        labelKey: 'platformDashboard.suspendedSubscriptions',
        hintKey: 'platformDashboard.suspendedHint',
        icon: 'pause_circle', tone: 'warn',
        count: data.suspendedSubscriptions ?? 0
      },
      {
        labelKey: 'platformDashboard.atCarQuota',
        hintKey: 'platformDashboard.atCarQuotaHint',
        icon: 'directions_car', tone: 'info',
        count: data.agenciesAtCarQuota ?? 0
      },
      {
        labelKey: 'platformDashboard.atClientQuota',
        hintKey: 'platformDashboard.atClientQuotaHint',
        icon: 'group', tone: 'info',
        count: data.agenciesAtClientQuota ?? 0
      }
    ];

    return all.filter(alert => alert.count > 0);
  }
}
