using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Credit.Queries;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Dashboard.Queries.GetTodayQuery
{
    /// <summary>
    /// Everything the agency's landing screen shows, in one answer.
    /// <para>
    /// The screen asks three questions — what does today ask for, what is waiting
    /// on somebody, and what is the fleet doing — and they are read together. Eight
    /// separate calls would give the desk eight different moments in time, and a
    /// figure in the header that disagrees with the row underneath it is worse than
    /// a slightly slower page. So: one query, one instant.
    /// </para>
    /// <para>
    /// This is the landing screen, so every signed-in member of the agency reaches
    /// it and there is no request-level gate: each SECTION is gated on the module
    /// it reads, imperatively (see <see cref="Entitlements"/> for why an attribute
    /// cannot say "and also, only if"). A section the caller may not see is omitted
    /// entirely rather than zeroed, and the screen then draws no card for it — the
    /// same rule the navigation applies, so nothing on the home page leads
    /// somewhere that would 403. Nothing here is actionable either; the links lead
    /// to records that enforce their own permissions.
    /// </para>
    /// </summary>
    public record GetTodayQuery(
        // The desk's calendar day, as UTC midnight. The browser sends its own —
        // the API's dates are wall-clock values stamped UTC (see the SPA's
        // form-utils), so a car booked out on the 5th belongs to the 5th whatever
        // the offset. Omitted ⇒ the server's day.
        DateTime? Day = null,
        // Scopes the fleet and the day's movements to one branch. A car has a home
        // branch; a hire and a hold are placed at their car's. Money owed by
        // clients is not a branch figure and stays agency-wide.
        int? BranchId = null
    ) : IRequest<TodayDto>;

    public class GetTodayQueryHandler : IRequestHandler<GetTodayQuery, TodayDto>
    {
        // The request card is a peek at the top of the queue, not the queue.
        private const int RequestRows = 5;
        // Per recurring cost. A group listing forty cars is a list screen, not a
        // card; the "see all" link on the section reaches the rest.
        private const int CarsPerExpenseGroup = 10;

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly IIdentityService _identity;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public GetTodayQueryHandler(
            IApplicationDbContext context,
            ITenantProvider tenant,
            IAgencySettingsProvider settings,
            IIdentityService identity,
            IUser user,
            TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _identity = identity;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<TodayDto> Handle(GetTodayQuery request, CancellationToken cancellationToken)
        {
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var settings = await _settings.GetAsync(agencyId, cancellationToken);
            var now = _dateTime.GetUtcNow().UtcDateTime;

            // Half-open [day, tomorrow), the app's one window convention.
            var day = (request.Day ?? now).Date;
            day = DateTime.SpecifyKind(day, DateTimeKind.Utc);
            var tomorrow = day.AddDays(1);

            var features = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
                _context, agencyId, _dateTime.GetUtcNow(), cancellationToken);

            // The same rule the navigation applies: feature on for the agency AND
            // the read permission held. An agency administrator passes every
            // permission policy by role, so this reduces to the feature for them.
            async Task<bool> CanAsync(string feature, string permission) =>
                features.Contains(feature) && await _identity.AuthorizeAsync(userId, permission);

            var canCars = await CanAsync(FeatureFlags.Cars, Permissions.CarRead);
            var canRentings = await CanAsync(FeatureFlags.Rentings, Permissions.RentingRead);
            var canReservations = await CanAsync(FeatureFlags.Reservations, Permissions.ReservationRead);
            var canExpenses = await CanAsync(FeatureFlags.Expenses, Permissions.ExpenseRead);

            // Money on either finance entitlement: the card carries both halves —
            // what is expected over the counter today (payments) and what clients
            // still owe (credits) — and holding one of the two is enough to be
            // shown the card. Whoever has neither gets no money on their home.
            var canMoney = await CanAsync(FeatureFlags.Credits, Permissions.CreditRead)
                           || await CanAsync(FeatureFlags.Payments, Permissions.PaymentRead);

            var branchId = request.BranchId;

            return new TodayDto
            {
                Currency = settings.CurrencyCode,
                Day = day,
                BranchId = branchId,
                Branches = features.Contains(FeatureFlags.Branches)
                    ? await BranchesAsync(cancellationToken)
                    : new List<TodayBranchDto>(),
                Fleet = canCars ? await FleetAsync(branchId, cancellationToken) : null,
                Summary = await SummaryAsync(
                    day, tomorrow, now, branchId, canRentings, canReservations, cancellationToken),
                Money = canMoney
                    ? await MoneyAsync(day, tomorrow, branchId, settings.CurrencyCode, cancellationToken)
                    : null,
                Requests = canReservations ? await RequestsAsync(branchId, cancellationToken) : null,
                Payables = canExpenses
                    ? await PayablesAsync(branchId, settings.CurrencyCode, cancellationToken)
                    : null,
                ExpensesDue = canExpenses && canCars
                    ? await ExpensesDueAsync(settings, now, branchId, cancellationToken)
                    : null,
            };
        }

        // --- Sections -----------------------------------------------------------

        private async Task<IList<TodayBranchDto>> BranchesAsync(CancellationToken cancellationToken) =>
            await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new TodayBranchDto { Id = b.Id, Name = b.Name ?? string.Empty })
                .ToListAsync(cancellationToken);

        private async Task<TodayFleetDto> FleetAsync(int? branchId, CancellationToken cancellationToken)
        {
            var cars = _context.Cars.AsNoTracking();

            if (branchId is int branch)
            {
                cars = cars.Where(c => c.BranchId == branch);
            }

            var total = await cars.CountAsync(cancellationToken);
            var bookable = await cars.CountAsync(c => c.Status == CarStatus.Active, cancellationToken);

            // Counted off the hires rather than off a flag on the car: "out" is a
            // fact about the booking, and a status column would be a second copy of
            // it to keep in step.
            var onRent = await cars
                .Where(c => c.Status == CarStatus.Active
                            && c.Rentings!.Any(r => r.RentingState == RentingState.InProgress))
                .CountAsync(cancellationToken);

            return new TodayFleetDto
            {
                Total = total,
                Free = bookable - onRent,
                OnRent = onRent,
                OutOfService = total - bookable,
            };
        }

        private async Task<TodaySummaryDto> SummaryAsync(
            DateTime day, DateTime tomorrow, DateTime now, int? branchId,
            bool canRentings, bool canReservations, CancellationToken cancellationToken)
        {
            int? bookingsToday = null;
            int? unconfirmedToday = null;
            int? returnsToday = null;
            int? returnsBeforeNoon = null;
            int? lateRentings = null;
            TodayLateRentingDto? worstLate = null;

            if (canRentings)
            {
                var rentings = Scoped(_context.Rentings.AsNoTracking(), branchId);

                bookingsToday = await rentings.CountAsync(
                    r => r.RentingState != RentingState.Cancelled
                         && r.StartDate >= day && r.StartDate < tomorrow, cancellationToken);

                var due = rentings.Where(r => r.RentingState == RentingState.InProgress
                                              && r.EndDate >= day && r.EndDate < tomorrow);

                returnsToday = await due.CountAsync(cancellationToken);
                returnsBeforeNoon = await due.CountAsync(
                    r => r.EndDate < day.AddHours(12), cancellationToken);

                var late = rentings.Where(r => r.RentingState == RentingState.InProgress
                                               && r.EndDate != null && r.EndDate < now);

                lateRentings = await late.CountAsync(cancellationToken);

                // Named on the card, so it has to be the one the desk would chase
                // first: the longest overdue.
                var worst = await late
                    .OrderBy(r => r.EndDate)
                    .Select(r => new
                    {
                        r.Id,
                        FirstName = r.Client == null ? null : r.Client.FirstName,
                        LastName = r.Client == null ? null : r.Client.LastName,
                        // The two halves of the car's label, NOT coalesced here:
                        // name columns carry the accent-insensitive collation and
                        // Matricule does not, so a CASE over the pair is a
                        // collation conflict SQL Server refuses outright.
                        Matricule = r.Car == null ? null : r.Car.Matricule,
                        ModelName = r.Car == null || r.Car.Model == null ? null : r.Car.Model.Name,
                        DueOn = r.EndDate!.Value,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (worst is not null)
                {
                    worstLate = new TodayLateRentingDto
                    {
                        RentingId = worst.Id,
                        ClientName = PersonLabel(worst.FirstName, worst.LastName),
                        CarLabel = CarLabel(worst.Matricule, worst.ModelName),
                        DueOn = worst.DueOn,
                        // Whole hours, computed here rather than on the screen: the
                        // browser's clock is minutes off the server's, and a card
                        // that says "0 h late" about a car an hour overdue is worse
                        // than no card.
                        HoursLate = Math.Max((int)(now - worst.DueOn).TotalHours, 0),
                    };
                }
            }

            if (canReservations)
            {
                var holds = Scoped(_context.Reservations.AsNoTracking(), branchId)
                    .Where(r => (r.Status == ReservationStatus.PendingConfirmation
                                 || r.Status == ReservationStatus.Confirmed
                                 || r.Status == ReservationStatus.Paid)
                                && r.StartDate >= day && r.StartDate < tomorrow);

                // Holds count towards the day's bookings alongside the hires: from
                // the counter they are the same job — a car leaving today.
                bookingsToday = (bookingsToday ?? 0) + await holds.CountAsync(cancellationToken);

                unconfirmedToday = await holds.CountAsync(
                    r => r.Status == ReservationStatus.PendingConfirmation, cancellationToken);
            }

            return new TodaySummaryDto
            {
                BookingsToday = bookingsToday,
                UnconfirmedToday = unconfirmedToday,
                ReturnsToday = returnsToday,
                ReturnsBeforeNoon = returnsBeforeNoon,
                LateRentings = lateRentings,
                WorstLate = worstLate,
            };
        }

        private async Task<TodayMoneyDto> MoneyAsync(
            DateTime day, DateTime tomorrow, int? branchId, string currency,
            CancellationToken cancellationToken)
        {
            var expected = await Scoped(_context.Rentings.AsNoTracking(), branchId)
                .Where(r => r.RentingState != RentingState.Cancelled
                            && r.Price != null
                            && r.StartDate >= day && r.StartDate < tomorrow)
                .SumAsync(r => r.Price!.Amount, cancellationToken);

            // Agency-wide on purpose: a client's balance is owed to the agency, not
            // to the branch the last car happened to come from.
            var outstanding = await _context.Clients
                .ToCreditRows()
                .Where(x => x.Charged - x.Paid > 0m)
                .SumAsync(x => x.Charged - x.Paid, cancellationToken);

            return new TodayMoneyDto
            {
                ExpectedToday = new MoneyDto(expected, currency),
                Outstanding = new MoneyDto(outstanding, currency),
            };
        }

        private async Task<TodayRequestsDto> RequestsAsync(
            int? branchId, CancellationToken cancellationToken)
        {
            var pending = Scoped(_context.Reservations.AsNoTracking(), branchId)
                .Where(r => r.Status == ReservationStatus.PendingConfirmation);

            var count = await pending.CountAsync(cancellationToken);

            if (count == 0)
            {
                return new TodayRequestsDto();
            }

            // "Waiting since" is when the request arrived, which is the audit stamp
            // — the only "asked at" the model has.
            var oldest = await pending.MinAsync(r => r.CreatedOn, cancellationToken);

            // Soonest pickup first: the request for tomorrow is the one that has to
            // be answered today, whatever order they arrived in.
            // The labels are assembled in memory (see the note in SummaryAsync):
            // coalescing Matricule with the model's name inside SQL puts two
            // different collations into one CASE, which SQL Server refuses.
            var rows = await pending
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.Id)
                .Take(RequestRows)
                .Select(r => new
                {
                    r.Id,
                    r.ClientId,
                    FirstName = r.Client == null ? null : r.Client.FirstName,
                    LastName = r.Client == null ? null : r.Client.LastName,
                    r.CarId,
                    Matricule = r.Car == null ? null : r.Car.Matricule,
                    ModelName = r.Car == null || r.Car.Model == null ? null : r.Car.Model.Name,
                    r.StartDate,
                    r.ExpiresAt,
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(r => new TodayRequestDto
                {
                    ReservationId = r.Id,
                    ClientId = r.ClientId,
                    ClientName = PersonLabel(r.FirstName, r.LastName),
                    CarId = r.CarId,
                    CarLabel = CarLabel(r.Matricule, r.ModelName),
                    StartDate = r.StartDate,
                    ExpiresAt = r.ExpiresAt,
                })
                .ToList();

            return new TodayRequestsDto { Count = count, OldestAskedAt = oldest, Items = items };
        }

        private async Task<TodayPayablesDto> PayablesAsync(
            int? branchId, string currency, CancellationToken cancellationToken)
        {
            // Unsettled: what the agency owes on a cost it has booked. PaidAmount is
            // a running total moved by the settlement command, not a set of rows
            // (see Expense.PaidAmount).
            var unpaid = _context.Expenses
                .AsNoTracking()
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseAmount.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount));

            if (branchId is int branch)
            {
                unpaid = unpaid.Where(e => e.Car != null && e.Car.BranchId == branch);
            }

            return new TodayPayablesDto
            {
                Count = await unpaid.CountAsync(cancellationToken),
                Outstanding = new MoneyDto(
                    await unpaid.SumAsync(
                        e => e.ExpenseAmount!.Amount - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount),
                        cancellationToken),
                    currency),
            };
        }

        // The fleet's maintenance and paperwork, read with the SAME rule the hourly
        // notification sweep uses (ExpenseDueCalculator): a type recurs every N
        // months or every N kilometres, counted from the last time that cost was
        // booked against the car. So the screen and the inbox never disagree about
        // what is due — and a car with no expense of the type yet has no baseline
        // and is silent, exactly as it is there.
        private async Task<IList<TodayExpenseGroupDto>> ExpensesDueAsync(
            AgencySettingsSnapshot settings, DateTime now, int? branchId,
            CancellationToken cancellationToken)
        {
            var types = await _context.ExpenseTypes
                .AsNoTracking()
                .Where(t => t.IsActive && t.WithNotif && (t.AfterMonth > 0 || t.AfterKilometer > 0))
                .Select(t => new { t.Id, t.Name, t.AfterMonth, t.AfterKilometer })
                .ToListAsync(cancellationToken);

            if (types.Count == 0)
            {
                return new List<TodayExpenseGroupDto>();
            }

            var typeIds = types.Select(t => t.Id).ToList();

            var carsQuery = _context.Cars
                .AsNoTracking()
                // A retired car is not maintained. Cars already in Maintenance stay
                // in: that is often exactly why they are off the road.
                .Where(c => c.Status != CarStatus.Inactive);

            if (branchId is int branch)
            {
                carsQuery = carsQuery.Where(c => c.BranchId == branch);
            }

            var cars = await carsQuery
                .Select(c => new
                {
                    c.Id,
                    c.Matricule,
                    c.Mileage,
                    ModelName = c.Model != null ? c.Model.Name : null,
                })
                .ToListAsync(cancellationToken);

            if (cars.Count == 0)
            {
                return new List<TodayExpenseGroupDto>();
            }

            var carsById = cars.ToDictionary(c => c.Id);
            var carIds = carsById.Keys.ToList();

            // MAX on both columns rather than "the values of the latest row": an
            // odometer only moves forward, so the highest reading recorded for a
            // type IS the last time it was done — and taking the maximum also lets
            // an older expense that does carry a reading serve as the distance
            // baseline when the newest one was recorded without.
            var baselines = await _context.Expenses
                .AsNoTracking()
                .Where(e => typeIds.Contains(e.ExpenseTypeId) && carIds.Contains(e.CarId))
                .GroupBy(e => new { e.CarId, e.ExpenseTypeId })
                .Select(g => new
                {
                    g.Key.CarId,
                    g.Key.ExpenseTypeId,
                    LastExpenseOn = g.Max(e => e.ExpenseDate),
                    LastMileage = g.Max(e => e.Mileage),
                })
                .ToListAsync(cancellationToken);

            var groups = new List<TodayExpenseGroupDto>();

            foreach (var type in types)
            {
                var due = new List<TodayExpenseCarDto>();

                foreach (var baseline in baselines.Where(b => b.ExpenseTypeId == type.Id))
                {
                    if (!carsById.TryGetValue(baseline.CarId, out var car))
                    {
                        continue;
                    }

                    var answer = ExpenseDueCalculator.Evaluate(
                        type.AfterMonth,
                        type.AfterKilometer,
                        baseline.LastExpenseOn,
                        baseline.LastMileage,
                        car.Mileage,
                        now,
                        settings.ExpenseDueLeadDays,
                        settings.ExpenseDueLeadKilometers);

                    if (answer is null)
                    {
                        continue;
                    }

                    due.Add(new TodayExpenseCarDto
                    {
                        CarId = car.Id,
                        Matricule = car.Matricule,
                        ModelName = car.ModelName,
                        Basis = answer.Basis,
                        IsOverdue = answer.IsOverdue,
                        DueOn = answer.DueOn,
                        Days = answer.Days,
                        DueAtKilometers = answer.DueAtKilometers,
                        Kilometers = answer.Kilometers,
                    });
                }

                if (due.Count == 0)
                {
                    continue;
                }

                groups.Add(new TodayExpenseGroupDto
                {
                    ExpenseTypeId = type.Id,
                    Name = type.Name ?? string.Empty,
                    AfterMonth = type.AfterMonth,
                    AfterKilometer = type.AfterKilometer,
                    IsOverdue = due.Any(c => c.IsOverdue),
                    // Worst first, so the truncation below drops the cars with the
                    // most room left rather than an arbitrary ten.
                    Cars = due
                        .OrderByDescending(c => c.IsOverdue)
                        .ThenByDescending(c => c.IsOverdue ? c.Days + (c.Kilometers ?? 0) : 0)
                        .ThenBy(c => c.IsOverdue ? 0 : c.Days + (c.Kilometers ?? 0))
                        .Take(CarsPerExpenseGroup)
                        .ToList(),
                });
            }

            // Overdue groups first; then the biggest job.
            return groups
                .OrderByDescending(g => g.IsOverdue)
                .ThenByDescending(g => g.Cars.Count)
                .ThenBy(g => g.Name)
                .ToList();
        }

        // --- Labels ---------------------------------------------------------------
        // Assembled here rather than in the SELECT. Name columns carry the
        // accent-insensitive collation (see the soft-delete/collation migration)
        // and plate columns do not, so any expression mixing the two — a CASE, a
        // concatenation — is a collation conflict the server refuses.

        /// <summary>The plate if the car has one, else the model; null for neither.</summary>
        private static string? CarLabel(string? matricule, string? modelName) =>
            string.IsNullOrWhiteSpace(matricule) ? Trimmed(modelName) : matricule.Trim();

        private static string? PersonLabel(string? firstName, string? lastName) =>
            Trimmed($"{firstName} {lastName}");

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // --- Branch scoping -------------------------------------------------------
        // A booking has no branch of its own: it is placed at its car's. A booking
        // whose car has been removed therefore belongs to no branch and drops out of
        // a branch-scoped view, which is the honest reading of "this branch's day".

        private static IQueryable<Domain.Entities.Renting> Scoped(
            IQueryable<Domain.Entities.Renting> query, int? branchId) =>
            branchId is int branch
                ? query.Where(r => r.Car != null && r.Car.BranchId == branch)
                : query;

        private static IQueryable<Domain.Entities.Reservation> Scoped(
            IQueryable<Domain.Entities.Reservation> query, int? branchId) =>
            branchId is int branch
                ? query.Where(r => r.Car != null && r.Car.BranchId == branch)
                : query;
    }
}
