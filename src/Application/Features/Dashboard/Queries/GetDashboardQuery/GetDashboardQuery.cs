using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Dashboard.Queries.GetDashboardQuery
{
    // The agency's overview screen. Every figure comes from the tenant-filtered
    // sets, so an agency only ever sees its own numbers. Read-only and counted
    // live rather than denormalised — an agency's data is small enough that the
    // aggregates are cheap, and a stale KPI is worse than a slightly slower one.
    [Authorize(Policy = Permissions.DashboardView)]
    [RequiresFeature(FeatureFlags.Dashboard)]
    public record GetDashboardQuery(
        // Window for the period-scoped figures; defaults to the current calendar
        // month when either bound is omitted.
        DateTime? From = null,
        DateTime? To = null,
        // Months of history in MonthlySeries, including the period's own month.
        int MonthsOfHistory = 6
    ) : IRequest<DashboardDto>;

    public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
    {
        private const int MaxMonthsOfHistory = 24;

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public GetDashboardQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
        {
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            var now = _dateTime.GetUtcNow().UtcDateTime;

            // Half-open window [start, end) — the same convention the booking
            // overlap rule uses, so a payment at midnight lands in one month only.
            var periodStart = request.From ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = request.To ?? periodStart.AddMonths(1);

            // --- Clients ---
            // Audit stamps are DateTimeOffset (unlike domain DateTimes, which are
            // UTC-enforced at the persistence boundary), so the window bounds are
            // lifted to UTC offsets to compare against CreatedOn.
            var periodStartOffset = new DateTimeOffset(periodStart, TimeSpan.Zero);
            var periodEndOffset = new DateTimeOffset(periodEnd, TimeSpan.Zero);

            var totalClients = await _context.Clients.CountAsync(cancellationToken);
            var newClientsInPeriod = await _context.Clients
                .CountAsync(c => c.CreatedOn >= periodStartOffset && c.CreatedOn < periodEndOffset,
                            cancellationToken);
            var flaggedClients = await _context.Clients.CountAsync(c => c.IsFlagged, cancellationToken);

            // --- Fleet ---
            var totalCars = await _context.Cars.CountAsync(cancellationToken);
            var activeCars = await _context.Cars
                .CountAsync(c => c.Status == CarStatus.Active, cancellationToken);
            var carsOnRent = await _context.Rentings
                .Where(r => r.RentingState == RentingState.InProgress && r.CarId != null)
                .Select(r => r.CarId)
                .Distinct()
                .CountAsync(cancellationToken);

            // --- Bookings ---
            var rentingsInProgress = await _context.Rentings
                .CountAsync(r => r.RentingState == RentingState.InProgress, cancellationToken);
            var rentingsUpcoming = await _context.Rentings
                .CountAsync(r => r.RentingState == RentingState.NotYet, cancellationToken);
            var pendingRequests = await _context.Reservations
                .CountAsync(r => r.Status == ReservationStatus.PendingConfirmation, cancellationToken);
            var returnsDueInPeriod = await _context.Rentings
                .CountAsync(r => r.RentingState == RentingState.InProgress
                                 && r.EndDate >= periodStart && r.EndDate < periodEnd, cancellationToken);

            // --- Money in the period ---
            var chargedInPeriod = await _context.Rentings
                .Where(r => r.RentingState != RentingState.Cancelled
                            && r.Price != null
                            && r.StartDate >= periodStart && r.StartDate < periodEnd)
                .SumAsync(r => r.Price!.Amount, cancellationToken);

            var collectedInPeriod = await _context.Payments
                .Where(p => p.PayementAmount != null
                            && p.PayementDate >= periodStart && p.PayementDate < periodEnd)
                .SumAsync(p => p.PayementAmount!.Amount, cancellationToken);

            var expensesInPeriod = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseDate >= periodStart && e.ExpenseDate < periodEnd)
                .SumAsync(e => e.ExpenseAmount!.Amount, cancellationToken);

            // --- All-time outstanding, both directions ---
            var debtors = _context.Clients
                .Select(c => new
                {
                    Charged =
                        c.Rentings!
                            .Where(r => r.RentingState != RentingState.Cancelled && r.Price != null)
                            .Sum(r => r.Price!.Amount)
                        + c.Reservations!
                            .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Paid)
                                        && r.Price != null)
                            .Sum(r => r.Price!.Amount),
                    Paid = c.Payments!
                        .Where(p => p.PayementAmount != null)
                        .Sum(p => p.PayementAmount!.Amount),
                })
                .Where(x => x.Charged - x.Paid > 0m);

            var clientsOutstanding = await debtors.SumAsync(x => x.Charged - x.Paid, cancellationToken);
            var clientsInDebtCount = await debtors.CountAsync(cancellationToken);

            var expensesOutstanding = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseAmount.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount))
                .SumAsync(e => e.ExpenseAmount!.Amount - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount),
                          cancellationToken);

            var monthlySeries = await BuildMonthlySeriesAsync(
                periodStart, request.MonthsOfHistory, currency, cancellationToken);

            return new DashboardDto
            {
                Currency = currency,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalClients = totalClients,
                NewClientsInPeriod = newClientsInPeriod,
                FlaggedClients = flaggedClients,
                ClientsInDebtCount = clientsInDebtCount,
                TotalCars = totalCars,
                ActiveCars = activeCars,
                CarsOnRent = carsOnRent,
                RentingsInProgress = rentingsInProgress,
                RentingsUpcoming = rentingsUpcoming,
                PendingReservationRequests = pendingRequests,
                ReturnsDueInPeriod = returnsDueInPeriod,
                ChargedInPeriod = new MoneyDto(chargedInPeriod, currency),
                CollectedInPeriod = new MoneyDto(collectedInPeriod, currency),
                ExpensesInPeriod = new MoneyDto(expensesInPeriod, currency),
                NetInPeriod = new MoneyDto(collectedInPeriod - expensesInPeriod, currency),
                ClientsOutstanding = new MoneyDto(clientsOutstanding, currency),
                ExpensesOutstanding = new MoneyDto(expensesOutstanding, currency),
                MonthlySeries = monthlySeries,
            };
        }

        // Collected vs booked-expenses per month over the trailing window. Months
        // with no activity are emitted as zeroes so the caller gets a contiguous
        // series and never has to fill gaps itself.
        private async Task<IList<DashboardMonthPointDto>> BuildMonthlySeriesAsync(
            DateTime periodStart, int monthsRequested, string currency, CancellationToken cancellationToken)
        {
            var months = Math.Clamp(monthsRequested, 1, MaxMonthsOfHistory);

            var seriesEnd = new DateTime(periodStart.Year, periodStart.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1);
            var seriesStart = seriesEnd.AddMonths(-months);

            var collected = await _context.Payments
                .Where(p => p.PayementAmount != null
                            && p.PayementDate >= seriesStart && p.PayementDate < seriesEnd)
                .GroupBy(p => new { p.PayementDate!.Value.Year, p.PayementDate!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.PayementAmount!.Amount) })
                .ToListAsync(cancellationToken);

            var spent = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseDate >= seriesStart && e.ExpenseDate < seriesEnd)
                .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(e => e.ExpenseAmount!.Amount) })
                .ToListAsync(cancellationToken);

            var points = new List<DashboardMonthPointDto>(months);

            for (var i = 0; i < months; i++)
            {
                var month = seriesStart.AddMonths(i);

                var monthCollected = collected
                    .FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Total ?? 0m;
                var monthSpent = spent
                    .FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Total ?? 0m;

                points.Add(new DashboardMonthPointDto
                {
                    Year = month.Year,
                    Month = month.Month,
                    Collected = new MoneyDto(monthCollected, currency),
                    Expenses = new MoneyDto(monthSpent, currency),
                });
            }

            return points;
        }
    }
}
