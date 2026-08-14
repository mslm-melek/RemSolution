using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Queries.GetCarExpenseSchedulesQuery
{
    /// <summary>
    /// Every notifiable expense type as it applies to one car. <paramref name="CarId"/>
    /// is optional so the new-car form can list the types with nothing linked yet.
    /// Gated on Expenses: these are recurring costs.
    /// </summary>
    [Authorize(Policy = Permissions.ExpenseRead)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record GetCarExpenseSchedulesQuery(int? CarId = null) : IRequest<IList<CarExpenseScheduleDto>>;

    public class GetCarExpenseSchedulesQueryHandler
        : IRequestHandler<GetCarExpenseSchedulesQuery, IList<CarExpenseScheduleDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public GetCarExpenseSchedulesQueryHandler(
            IApplicationDbContext context,
            IAgencySettingsProvider settings,
            ITenantProvider tenant,
            TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task<IList<CarExpenseScheduleDto>> Handle(
            GetCarExpenseSchedulesQuery request, CancellationToken cancellationToken)
        {
            var types = await _context.ExpenseTypes
                .AsNoTracking()
                .Where(t => t.IsActive && t.WithNotif)
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.AfterMonth, t.AfterKilometer })
                .ToListAsync(cancellationToken);

            if (types.Count == 0)
            {
                return new List<CarExpenseScheduleDto>();
            }

            var leadDays = 0;
            var leadKilometers = 0;

            if (_tenant.AgencyId is int agencyId)
            {
                var settings = await _settings.GetAsync(agencyId, cancellationToken);
                leadDays = settings.ExpenseDueLeadDays;
                leadKilometers = settings.ExpenseDueLeadKilometers;
            }

            var typeIds = types.Select(t => t.Id).ToList();

            var schedules = new List<CarExpenseScheduleValues>();
            var baselines = new List<CarExpenseBaseline>();
            int? carMileage = null;

            if (request.CarId is int carId)
            {
                // The tenant filter makes this a check rather than a leak.
                var car = await _context.Cars
                    .AsNoTracking()
                    .Where(c => c.Id == carId)
                    .Select(c => new { c.Id, c.Mileage })
                    .FirstOrDefaultAsync(cancellationToken);

                if (car is null)
                {
                    throw new NotFoundException(nameof(Domain.Entities.Car), carId.ToString());
                }

                carMileage = car.Mileage;

                schedules = await _context.CarExpenseSchedules
                    .AsNoTracking()
                    .Where(s => s.CarId == carId && typeIds.Contains(s.ExpenseTypeId))
                    .Select(s => new CarExpenseScheduleValues(
                        s.CarId, s.ExpenseTypeId, s.AfterMonth, s.AfterKilometer,
                        s.LeadDays, s.LeadKilometers, s.LastDoneOn, s.LastDoneMileage))
                    .ToListAsync(cancellationToken);

                // Same MAX fold as the sweep: latest date and highest reading per type.
                baselines = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.CarId == carId && typeIds.Contains(e.ExpenseTypeId))
                    .GroupBy(e => e.ExpenseTypeId)
                    .Select(g => new CarExpenseBaseline(
                        carId,
                        g.Key,
                        g.Max(e => e.ExpenseDate),
                        g.Max(e => e.Mileage)))
                    .ToListAsync(cancellationToken);
            }

            var now = _dateTime.GetUtcNow().UtcDateTime;
            var rows = new List<CarExpenseScheduleDto>();

            foreach (var type in types)
            {
                var custom = schedules.FirstOrDefault(s => s.ExpenseTypeId == type.Id);
                var baseline = baselines.FirstOrDefault(b => b.ExpenseTypeId == type.Id);

                // Through the planner, so the screen quotes what the sweep will act on.
                var plan = CarExpenseSchedules.Resolve(
                    request.CarId ?? 0,
                    new CarExpenseTypeRule(type.Id, type.AfterMonth, type.AfterKilometer),
                    baseline,
                    custom,
                    leadDays,
                    leadKilometers);

                var nextDueOn = ExpenseDueCalculator.NextDueOn(plan?.LastDoneOn, plan?.AfterMonths);
                var nextDueAt = ExpenseDueCalculator.NextDueAtKilometers(
                    plan?.LastDoneMileage, plan?.AfterKilometers);

                rows.Add(new CarExpenseScheduleDto
                {
                    ExpenseTypeId = type.Id,
                    ExpenseTypeName = type.Name,
                    IsLinked = custom is not null,
                    HasOwnInterval = plan?.IsCustom ?? false,
                    TypeAfterKilometer = type.AfterKilometer,
                    TypeAfterMonth = type.AfterMonth,
                    AfterKilometer = custom?.AfterKilometers,
                    AfterMonth = custom?.AfterMonths,
                    LeadKilometers = custom?.LeadKilometers,
                    LeadDays = custom?.LeadDays,
                    LastDoneMileage = custom?.LastDoneMileage,
                    LastDoneOn = custom?.LastDoneOn,
                    EffectiveAfterKilometer = plan?.AfterKilometers,
                    EffectiveAfterMonth = plan?.AfterMonths,
                    BaselineOn = plan?.LastDoneOn,
                    BaselineMileage = plan?.LastDoneMileage,
                    NextDueOn = nextDueOn,
                    NextDueAtKilometers = nextDueAt,
                    // Either clock being past counts; the warning window does not apply here.
                    IsOverdue = (nextDueOn is DateTime due && due.Date < now.Date)
                        || (nextDueAt is int at && carMileage is int mileage && mileage > at),
                });
            }

            return rows;
        }
    }
}
