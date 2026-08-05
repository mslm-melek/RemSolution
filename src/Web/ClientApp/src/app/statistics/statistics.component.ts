import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import {
  StatisticsClient, StatisticsDto, StatisticsGranularity, StatisticsRowDto
} from '../web-api-client';
import { applyListFilters, enumName, enumParam, idParam } from '../shared/list-filters';
import { fromDateInput } from '../shared/form-utils';

// How many years back the year picker offers. Five plus the current one covers
// the history an agency has and keeps the select short.
const YEARS_OFFERED = 5;

@Component({
  selector: 'app-statistics',
  templateUrl: './statistics.component.html',
  styleUrls: ['./statistics.component.css']
})
export class StatisticsComponent implements OnInit {
  data?: StatisticsDto;
  loading = true;
  errorMessage = '';

  // Both filters live in the URL (see list-filters): the car list and a car's own
  // page link straight here with ?car=, and a report kept in the URL survives a
  // reload and can be sent to somebody else.
  carId: number | null = null;
  granularity: StatisticsGranularity = StatisticsGranularity.Month;
  // Which calendar year the monthly table covers. Meaningless for the yearly
  // table, which always shows the trailing years.
  year = new Date().getUTCFullYear();

  years: number[] = [];

  // Returned instead of allocating one per read: the template asks for the
  // totals row several times per change-detection pass.
  private static readonly EMPTY_ROW = new StatisticsRowDto();

  // The period table's own row identity is its bucket; the per-car table's is its
  // car. Same columns otherwise — the figures are the same six numbers.
  readonly periodColumns = ['period', 'rentings', 'days', 'charged', 'collected', 'expenses', 'net'];
  readonly carColumns = ['car', 'rentings', 'days', 'charged', 'collected', 'expenses', 'net', 'actions'];

  // Biggest revenue figure in each table, the bars are drawn against it.
  periodScale = 0;
  carScale = 0;

  readonly granularityOptions = [
    { value: StatisticsGranularity.Month, labelKey: 'statistics.byMonth' },
    { value: StatisticsGranularity.Year, labelKey: 'statistics.byYear' }
  ];

  constructor(
    private statistics: StatisticsClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    const thisYear = new Date().getUTCFullYear();
    for (let y = thisYear; y > thisYear - YEARS_OFFERED - 1; y--) this.years.push(y);
  }

  ngOnInit() {
    // The URL is the single source of truth: the controls below navigate, and this
    // subscription is what actually loads. One path in, whether the screen was
    // opened from a link or a select was changed.
    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);
      this.load();
    });
  }

  private readFilters(params: ParamMap) {
    this.carId = idParam(params, 'car');
    this.granularity = (enumParam(params, 'granularity', StatisticsGranularity)
      ?? StatisticsGranularity.Month) as StatisticsGranularity;

    const year = Number(params.get('year'));
    this.year = Number.isInteger(year) && year > 1990 && year < 3000
      ? year
      : new Date().getUTCFullYear();

    // A link can carry a year older than the picker offers (an emailed report, a
    // bookmark). It joins the list rather than leaving the select blank.
    if (!this.years.includes(this.year)) {
      this.years = [...this.years, this.year].sort((a, b) => b - a);
    }
  }

  private load() {
    this.loading = true;
    this.errorMessage = '';

    // A month-by-month table is one calendar year; a year-by-year table is the
    // trailing years the API defaults to, so it sends no window at all.
    const monthly = this.granularity === StatisticsGranularity.Month;
    const from = monthly ? fromDateInput(`${this.year}-01-01`) ?? null : null;
    const to = monthly ? fromDateInput(`${this.year + 1}-01-01`) ?? null : null;

    this.statistics.getStatistics(this.carId, this.granularity, from, to).subscribe({
      next: data => {
        this.data = data;
        this.periodScale = StatisticsComponent.scale(data.periods);
        this.carScale = StatisticsComponent.scale(data.byCar);
        this.loading = false;
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'statistics.loadFailed';
        this.loading = false;
      }
    });
  }

  // --- Controls (they navigate; the subscription above reloads) ---------------

  selectGranularity(value: StatisticsGranularity) {
    this.navigate({ granularity: enumName(StatisticsGranularity, value) });
  }

  selectYear(value: number) {
    this.navigate({ year: String(value) });
  }

  selectCar(value: number | null) {
    this.navigate({ car: value === null ? null : String(value) });
  }

  /** The current filters with one changed — a null value clears that filter. */
  private navigate(change: Params) {
    const params: Params = {
      car: this.carId === null ? null : String(this.carId),
      granularity: enumName(StatisticsGranularity, this.granularity),
      year: String(this.year),
      ...change
    };

    // Defaults are left out rather than spelled out, so the menu's plain
    // /statistics link, the car list's ?car= link and this screen's own controls
    // all produce the same URL for the same report.
    if (params['granularity'] === 'Month') delete params['granularity'];
    // The year only scopes the monthly table, and only when it is not this year.
    if (params['granularity'] || params['year'] === String(new Date().getUTCFullYear())) {
      delete params['year'];
    }
    for (const key of Object.keys(params)) {
      if (params[key] === null) delete params[key];
    }

    applyListFilters(this.router, this.route, params);
  }

  // --- Reading the answer ----------------------------------------------------

  get isMonthly(): boolean {
    return this.granularity === StatisticsGranularity.Month;
  }

  /** The window's own row. Never null, so the tiles and the totals line can read
   *  it without a chain of optional accesses in the template. */
  get totals(): StatisticsRowDto {
    return this.data?.totals ?? StatisticsComponent.EMPTY_ROW;
  }

  get periods(): StatisticsRowDto[] {
    return this.data?.periods ?? [];
  }

  get byCar(): StatisticsRowDto[] {
    return this.data?.byCar ?? [];
  }

  /** The fleet table only says something the period table does not for the fleet view. */
  get showByCar(): boolean {
    return this.carId === null && this.byCar.length > 0 && this.hasActivity;
  }

  get hasActivity(): boolean {
    const totals = this.data?.totals;
    return !!totals && (
      (totals.rentings ?? 0) > 0
      || (totals.charged?.amount ?? 0) !== 0
      || (totals.collected?.amount ?? 0) !== 0
      || (totals.expenses?.amount ?? 0) !== 0);
  }

  /**
   * A period row's label as a local Date the template's date pipe can format in
   * the active language. Rebuilt from the bucket's UTC parts and pinned to midday:
   * the buckets are UTC midnights, and formatting one through a negative offset
   * would label January's row "December".
   */
  periodDate(row: StatisticsRowDto): Date | null {
    const start = row.bucketStart;
    if (!start) return null;
    return new Date(start.getUTCFullYear(), start.getUTCMonth(), 1, 12);
  }

  periodYear(row: StatisticsRowDto): number | null {
    return row.bucketStart ? row.bucketStart.getUTCFullYear() : null;
  }

  /**
   * Bar width for the revenue column, as a share of the table's best row. The two
   * scales are computed once per load rather than in the getter the template would
   * otherwise call on every change-detection pass.
   */
  revenueShare(row: StatisticsRowDto, scale: number): number {
    if (!scale) return 0;
    return Math.round((Math.abs(row.charged?.amount ?? 0) / scale) * 100);
  }

  netClass(row: StatisticsRowDto): string {
    const net = row.net?.amount ?? 0;
    if (net > 0) return 'good';
    return net < 0 ? 'bad' : '';
  }

  private static scale(rows?: StatisticsRowDto[]): number {
    return (rows ?? []).reduce((best, row) => Math.max(best, Math.abs(row.charged?.amount ?? 0)), 0);
  }
}
