import { Component, OnInit, inject } from '@angular/core';
import {
  DashboardClient, DashboardDto, DashboardGranularity, DashboardPeriodPointDto
} from '../web-api-client';
import { extractValidationErrors, fromDateInput, toDateInput } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

// The windows the screen offers. The API takes an arbitrary [from, to), so these
// are only presets — `custom` hands the two dates straight to it.
type PeriodKey = 'thisMonth' | 'lastMonth' | 'last3Months' | 'thisYear' | 'lastYear' | 'custom';

// Which figures the trend chart draws. Money is two lines (in vs out); the rest
// are single counts, and mixing a count onto the money axis would flatten both.
type MetricKey = 'money' | 'cars' | 'clients' | 'rentings';

interface Period {
  from: Date;
  to: Date;
}

// One line of the chart: where its value comes from and which colour class the
// template gives it.
interface MetricLine {
  key: 'collected' | 'spent' | 'primary';
  labelKey: string;
  pick: (point: DashboardPeriodPointDto) => number;
}

interface Metric {
  key: MetricKey;
  labelKey: string;
  isMoney: boolean;
  lines: MetricLine[];
}

// One point of one line, in SVG user units, carrying the text for its tooltip.
interface ChartPoint {
  x: number;
  y: number;
  value: number;
  label: string;
}

interface ChartSeries {
  key: string;
  labelKey: string;
  line: string;
  area: string;
  points: ChartPoint[];
}

// Everything the template needs to draw the trend chart. Built once per load
// rather than in getters: a template getter would recompute on every change
// detection pass.
interface Chart {
  width: number;
  height: number;
  // Plot bounds, so the template does not repeat the padding constants.
  plotLeft: number;
  plotRight: number;
  labelX: number;
  bucketLabelY: number;
  // Draw order — reversed, so the first line of a two-line metric ends up on top
  // of the second rather than under it.
  series: ChartSeries[];
  // Declaration order, for the legend: the key figure reads first.
  legend: ChartSeries[];
  gridLines: { y: number; label: string }[];
  bucketLabels: { x: number; text: string }[];
  hasData: boolean;
  isMoney: boolean;
}

// An item on the "needs attention" list. Only non-zero ones are rendered, so an
// agency with nothing outstanding sees an empty-state instead of six zeroes.
interface Alert {
  labelKey: string;
  icon: string;
  count: number;
  tone: 'danger' | 'warn' | 'info';
  link: string;
  actionKey: string;
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  data?: DashboardDto;
  loading = true;
  errorMessage = '';

  periodKey: PeriodKey = 'thisMonth';
  // Bound to the two date inputs, inclusive at both ends — the way a person
  // reads "1 to 30 April". Converted to the API's half-open window on send.
  customFrom = '';
  customTo = '';
  customError = '';

  granularity: DashboardGranularity = DashboardGranularity.Month;
  metricKey: MetricKey = 'money';

  chart?: Chart;
  alerts: Alert[] = [];

  // Chart geometry, in SVG user units; the <svg> scales to its container.
  private static readonly W = 760;
  private static readonly H = 230;
  private static readonly PAD = { left: 52, right: 14, top: 16, bottom: 30 };

  // How much history each bucket size shows. A function of the granularity alone,
  // so the answer to "how far back does this chart go" does not also depend on
  // which window preset happens to be selected.
  private static readonly BUCKETS: Record<DashboardGranularity, number> = {
    [DashboardGranularity.Day]: 30,
    [DashboardGranularity.Month]: 12,
    [DashboardGranularity.Year]: 5,
  };

  readonly periodOptions: { key: PeriodKey; labelKey: string }[] = [
    { key: 'thisMonth', labelKey: 'dashboard.periodThisMonth' },
    { key: 'lastMonth', labelKey: 'dashboard.periodLastMonth' },
    { key: 'last3Months', labelKey: 'dashboard.periodLast3' },
    { key: 'thisYear', labelKey: 'dashboard.periodThisYear' },
    { key: 'lastYear', labelKey: 'dashboard.periodLastYear' },
    { key: 'custom', labelKey: 'dashboard.periodCustom' }
  ];

  readonly granularityOptions: { value: DashboardGranularity; labelKey: string }[] = [
    { value: DashboardGranularity.Day, labelKey: 'dashboard.byDay' },
    { value: DashboardGranularity.Month, labelKey: 'dashboard.byMonth' },
    { value: DashboardGranularity.Year, labelKey: 'dashboard.byYear' }
  ];

  readonly metrics: Metric[] = [
    {
      key: 'money', labelKey: 'dashboard.metricMoney', isMoney: true,
      lines: [
        { key: 'collected', labelKey: 'dashboard.collected', pick: p => p.collected?.amount ?? 0 },
        { key: 'spent', labelKey: 'dashboard.expenses', pick: p => p.expenses?.amount ?? 0 }
      ]
    },
    {
      key: 'cars', labelKey: 'dashboard.metricCars', isMoney: false,
      lines: [{ key: 'primary', labelKey: 'dashboard.newCars', pick: p => p.newCars ?? 0 }]
    },
    {
      key: 'clients', labelKey: 'dashboard.metricClients', isMoney: false,
      lines: [{ key: 'primary', labelKey: 'dashboard.newClients', pick: p => p.newClients ?? 0 }]
    },
    {
      key: 'rentings', labelKey: 'dashboard.metricRentings', isMoney: false,
      lines: [{ key: 'primary', labelKey: 'dashboard.rentingsStarted', pick: p => p.rentingsStarted ?? 0 }]
    }
  ];

  // Referenced by the template for the granularity toggle's values.
  readonly Granularity = DashboardGranularity;

  constructor(private client: DashboardClient) { }

  ngOnInit() {
    const today = new Date();
    // Seed the custom inputs with the current month, so switching to it shows a
    // sensible range instead of two empty fields.
    this.customFrom = toDateInput(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1)));
    this.customTo = toDateInput(today);
    this.load();
  }

  // --- Controls -------------------------------------------------------------

  selectPeriod(key: PeriodKey) {
    if (key === this.periodKey) return;
    this.periodKey = key;
    // Switching to the custom range loads straight away rather than waiting for
    // Apply: the two inputs are seeded with the current month, so there is always
    // a usable range to show. Apply is for changing it afterwards.
    this.load();
  }

  selectGranularity(granularity: DashboardGranularity) {
    if (granularity === this.granularity) return;
    this.granularity = granularity;
    this.load();
  }

  selectMetric(key: MetricKey) {
    if (key === this.metricKey) return;
    this.metricKey = key;
    // Only the chart changes — no need to go back to the API for it.
    if (this.data) this.chart = this.buildChart(this.data);
  }

  applyCustomRange() {
    this.periodKey = 'custom';
    this.load();
  }

  get activeMetric(): Metric {
    return this.metrics.find(m => m.key === this.metricKey) ?? this.metrics[0];
  }

  load() {
    const period = this.resolvePeriod();

    if (!period) {
      // A backwards or incomplete custom range: say so and keep the last figures
      // on screen rather than blanking them.
      this.customError = this.transloco.translate('dashboard.customRangeInvalid');
      return;
    }

    this.customError = '';
    this.loading = true;
    this.errorMessage = '';

    this.client.getDashboard(
      period.from, period.to, DashboardComponent.BUCKETS[this.granularity], this.granularity
    ).subscribe({
      next: data => {
        this.data = data;
        this.chart = this.buildChart(data);
        this.alerts = this.buildAlerts(data);
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

  // --- Period ---------------------------------------------------------------

  // Bounds are built in UTC and half-open [from, to), matching the query. Null
  // when the custom range is not usable.
  private resolvePeriod(): Period | null {
    const now = new Date();
    const year = now.getUTCFullYear();
    const month = now.getUTCMonth();
    const monthStart = (y: number, m: number) => new Date(Date.UTC(y, m, 1));

    switch (this.periodKey) {
      case 'lastMonth':
        return { from: monthStart(year, month - 1), to: monthStart(year, month) };
      case 'last3Months':
        return { from: monthStart(year, month - 2), to: monthStart(year, month + 1) };
      case 'thisYear':
        return { from: monthStart(year, 0), to: monthStart(year + 1, 0) };
      case 'lastYear':
        return { from: monthStart(year - 1, 0), to: monthStart(year, 0) };
      case 'custom':
        return this.resolveCustomPeriod();
      default:
        return { from: monthStart(year, month), to: monthStart(year, month + 1) };
    }
  }

  private resolveCustomPeriod(): Period | null {
    const from = fromDateInput(this.customFrom);
    const to = fromDateInput(this.customTo);

    if (!from || !to) return null;

    // The inputs are inclusive; the API window is half-open, so the last day is
    // included by ending the window at the start of the next one.
    const exclusiveTo = new Date(Date.UTC(
      to.getUTCFullYear(), to.getUTCMonth(), to.getUTCDate() + 1));

    return exclusiveTo > from ? { from, to: exclusiveTo } : null;
  }

  // --- Derived figures ------------------------------------------------------

  // Share of what was invoiced in the period that actually came in. Null when
  // nothing was charged: 0/0 is not "0% collected".
  get collectionRate(): number | null {
    const charged = this.data?.chargedInPeriod?.amount ?? 0;
    if (charged <= 0) return null;
    return ((this.data?.collectedInPeriod?.amount ?? 0) / charged) * 100;
  }

  // Share of the bookable fleet that is out on a renting right now.
  get utilization(): number | null {
    const active = this.data?.activeCars ?? 0;
    if (active <= 0) return null;
    return ((this.data?.carsOnRent ?? 0) / active) * 100;
  }

  get netIsNegative(): boolean {
    return (this.data?.netInPeriod?.amount ?? 0) < 0;
  }

  // Change across the last two buckets of the series, which is what the chart is
  // already showing. Null when there is no prior bucket or the prior one was zero
  // (a rise from nothing is not a percentage).
  get collectedChange(): number | null {
    return this.bucketOverBucket(p => p.collected?.amount ?? 0);
  }

  get expensesChange(): number | null {
    return this.bucketOverBucket(p => p.expenses?.amount ?? 0);
  }

  private bucketOverBucket(pick: (point: DashboardPeriodPointDto) => number): number | null {
    const series = this.data?.series ?? [];
    if (series.length < 2) return null;

    const previous = pick(series[series.length - 2]);
    const current = pick(series[series.length - 1]);
    if (previous <= 0) return null;

    return ((current - previous) / previous) * 100;
  }

  // Average result over the buckets the chart covers — a steadier number than a
  // single bucket's net.
  get averageNetPerBucket(): number {
    const series = this.data?.series ?? [];
    if (!series.length) return 0;

    const total = series.reduce(
      (sum, point) => sum + (point.collected?.amount ?? 0) - (point.expenses?.amount ?? 0), 0);
    return total / series.length;
  }

  // The bucket size names the comparison and the average, so a chart of years
  // does not claim to show a monthly figure.
  get vsPreviousLabelKey(): string {
    switch (this.data?.granularity ?? this.granularity) {
      case DashboardGranularity.Day: return 'dashboard.vsPrevDay';
      case DashboardGranularity.Year: return 'dashboard.vsPrevYear';
      default: return 'dashboard.vsPrevMonth';
    }
  }

  get averageLabelKey(): string {
    switch (this.data?.granularity ?? this.granularity) {
      case DashboardGranularity.Day: return 'dashboard.avgDailyNet';
      case DashboardGranularity.Year: return 'dashboard.avgYearlyNet';
      default: return 'dashboard.avgMonthlyNet';
    }
  }

  // --- Chart ---------------------------------------------------------------

  private buildChart(data: DashboardDto): Chart {
    const { W, H, PAD } = DashboardComponent;
    const metric = this.activeMetric;
    const points = data.series ?? [];
    const plotWidth = W - PAD.left - PAD.right;
    const plotHeight = H - PAD.top - PAD.bottom;

    const values: number[] = [];
    for (const point of points) {
      for (const line of metric.lines) {
        values.push(line.pick(point));
      }
    }

    const peak = Math.max(0, ...values);
    // Counts are whole things, so their axis is stepped in whole things too —
    // gridlines reading 0/1/2/3 instead of 0/0.75/1.5.
    const max = metric.isMoney ? this.niceCeiling(peak) : Math.max(1, Math.ceil(this.niceCeiling(peak)));

    // A single bucket has no line to draw between points, so it is placed in the
    // middle of the plot instead of at its left edge.
    const x = (index: number) => points.length > 1
      ? PAD.left + (index / (points.length - 1)) * plotWidth
      : PAD.left + plotWidth / 2;
    const y = (value: number) => PAD.top + plotHeight - (value / max) * plotHeight;

    const build = (line: MetricLine): ChartSeries => {
      const series: ChartPoint[] = points.map((point, index) => ({
        x: x(index),
        y: y(line.pick(point)),
        value: line.pick(point),
        label: this.bucketLabel(point)
      }));

      const path = series.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');
      const baseline = PAD.top + plotHeight;
      const area = series.length
        ? `${path} L${series[series.length - 1].x.toFixed(1)} ${baseline} L${series[0].x.toFixed(1)} ${baseline} Z`
        : '';

      return { key: line.key, labelKey: line.labelKey, line: path, area, points: series };
    };

    const lines = metric.lines.map(build);

    return {
      width: W,
      height: H,
      plotLeft: PAD.left,
      plotRight: W - PAD.right,
      labelX: PAD.left - 8,
      bucketLabelY: H - 8,
      series: [...lines].reverse(),
      legend: lines,
      gridLines: [0, 0.25, 0.5, 0.75, 1].map(ratio => ({
        y: PAD.top + plotHeight - ratio * plotHeight,
        label: metric.isMoney
          ? this.compact(max * ratio)
          // A count axis only labels whole steps; the rest are left blank rather
          // than repeating "1" four times on a chart whose peak is 2.
          : (Number.isInteger(max * ratio) ? String(max * ratio) : '')
      })),
      bucketLabels: this.thin(points).map(({ point, index }) => ({
        x: x(index), text: this.bucketLabel(point)
      })),
      hasData: peak > 0,
      isMoney: metric.isMoney
    };
  }

  // Thirty daily labels will not fit across the axis, so every nth is kept —
  // always including the last, which is the bucket the figures above refer to.
  private thin(points: DashboardPeriodPointDto[]): { point: DashboardPeriodPointDto; index: number }[] {
    const maxLabels = 12;
    const step = Math.ceil(points.length / maxLabels);

    return points
      .map((point, index) => ({ point, index }))
      .filter(({ index }) => index % step === 0 || index === points.length - 1);
  }

  // Rounds the axis top up to 1/2/5 × a power of ten, so the gridline labels
  // are readable numbers instead of 37,214.83.
  private niceCeiling(value: number): number {
    if (value <= 0) return 1;

    const magnitude = Math.pow(10, Math.floor(Math.log10(value)));
    const normalized = value / magnitude;
    const step = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
    return step * magnitude;
  }

  private compact(value: number): string {
    if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
    if (value >= 1_000) return `${Math.round(value / 1_000)}k`;
    return String(Math.round(value));
  }

  // Tooltip text for a chart point: "Mar — 12 400.00 TND", or "Mar — 3" for a count.
  pointTitle(point: ChartPoint): string {
    if (!this.activeMetric.isMoney) {
      return `${point.label} — ${point.value}`;
    }

    const amount = point.value.toLocaleString(this.transloco.getActiveLang(), {
      minimumFractionDigits: 2, maximumFractionDigits: 2
    });
    return `${point.label} — ${amount} ${this.data?.currency ?? ''}`;
  }

  // A bucket reads as its own size: a day as "12 Apr", a month as "Apr" (with the
  // year when the series crosses one), a year as "2030".
  bucketLabel(point: DashboardPeriodPointDto): string {
    if (!point.bucketStart) return '';

    const date = new Date(point.bucketStart);
    const lang = this.transloco.getActiveLang();

    switch (this.data?.granularity ?? this.granularity) {
      case DashboardGranularity.Day:
        return date.toLocaleDateString(lang, { day: 'numeric', month: 'short', timeZone: 'UTC' });
      case DashboardGranularity.Year:
        return date.toLocaleDateString(lang, { year: 'numeric', timeZone: 'UTC' });
      default:
        return date.toLocaleDateString(lang, {
          month: 'short',
          year: this.seriesCrossesAYear ? '2-digit' : undefined,
          timeZone: 'UTC'
        });
    }
  }

  private get seriesCrossesAYear(): boolean {
    const series = this.data?.series ?? [];
    if (series.length < 2) return false;

    const years = series.map(p => p.bucketStart ? new Date(p.bucketStart).getUTCFullYear() : 0);
    return Math.min(...years) !== Math.max(...years);
  }

  // --- Needs attention -----------------------------------------------------

  private buildAlerts(data: DashboardDto): Alert[] {
    const all: Alert[] = [
      {
        labelKey: 'dashboard.pendingRequests', icon: 'inbox', tone: 'warn',
        count: data.pendingReservationRequests ?? 0,
        link: '/reservation', actionKey: 'dashboard.reviewRequests'
      },
      {
        labelKey: 'dashboard.returnsDue', icon: 'assignment_return', tone: 'info',
        count: data.returnsDueInPeriod ?? 0,
        link: '/renting', actionKey: 'dashboard.goToRentings'
      },
      {
        labelKey: 'dashboard.clientsInDebt', icon: 'account_balance_wallet', tone: 'danger',
        count: data.clientsInDebtCount ?? 0,
        link: '/credit', actionKey: 'dashboard.goToCredits'
      },
      {
        labelKey: 'dashboard.flaggedClients', icon: 'flag', tone: 'danger',
        count: data.flaggedClients ?? 0,
        link: '/client', actionKey: 'dashboard.goToClients'
      }
    ];

    return all.filter(alert => alert.count > 0);
  }
}
