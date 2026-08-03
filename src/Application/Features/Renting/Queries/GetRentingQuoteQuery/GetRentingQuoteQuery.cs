using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingQuoteQuery
{
    // What a car costs for a period, before anything is written — the figure the
    // booking screen shows while the agent is still picking dates.
    //
    // Read-only and side-effect free: it prices through IPricingService, exactly
    // like the create handler will, so the number on screen is the number that
    // gets stored (unless the agent deliberately overrides it — see
    // CreateRentingCommand.PriceOverride). Keeping the arithmetic on this side of
    // the wire is what makes that promise hold: the SPA never multiplies a rate by
    // a duration on its own, so seasonal or promotional pricing added to
    // IPricingService later shows up in the quote for free.
    //
    // It also answers "is the car actually free then?" through the same
    // IAvailabilityChecker the write path uses, so the agent learns about a clash
    // while choosing the dates rather than from a 409 after filling in the whole
    // form. The check is advisory only: it runs outside the per-agency write lock,
    // so the create handler re-checks under the lock and remains the authority.
    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingQuoteQuery(
        int CarId,
        DateTime StartDate,
        DateTime EndDate,
        // The booking being edited, excluded from the availability check so a
        // renting never reports itself as a clash.
        int? ExcludeRentingId = null
    ) : IRequest<RentingQuoteDto>;

    public class GetRentingQuoteQueryHandler : IRequestHandler<GetRentingQuoteQuery, RentingQuoteDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;
        private readonly IAgencySettingsProvider _settings;

        public GetRentingQuoteQueryHandler(
            IApplicationDbContext context,
            IPricingService pricing,
            IAvailabilityChecker availability,
            IAgencySettingsProvider settings)
        {
            _context = context;
            _pricing = pricing;
            _availability = availability;
            _settings = settings;
        }

        public async Task<RentingQuoteDto> Handle(
            GetRentingQuoteQuery request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            var settings = await _settings.GetAsync(car.AgencyId, cancellationToken);

            // A quote is asked for while the dates are still being typed, so an
            // end date that is not yet after the start is an ordinary state of the
            // form — reported as "nothing to price", not as a validation failure.
            if (request.EndDate <= request.StartDate)
            {
                return new RentingQuoteDto
                {
                    CarId = car.Id,
                    Currency = settings.CurrencyCode,
                    DailyRate = car.DailyRate is { } rate ? MoneyDto.From(rate) : null,
                    CarStatus = car.Status,
                    IsCarBookable = car.Status == CarStatus.Active,
                    IsAvailable = false,
                };
            }

            var billedDays = (int)Math.Ceiling((request.EndDate - request.StartDate).TotalDays);

            // Unpriced cars are still quotable: the answer is "no automatic price",
            // which is precisely when the agent types one in.
            var price = car.DailyRate is null
                ? null
                : _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);

            var conflict = false;

            try
            {
                await _availability.EnsureCarAvailableAsync(
                    request.CarId, request.StartDate, request.EndDate,
                    excludeRentingId: request.ExcludeRentingId,
                    excludeReservationId: null,
                    cancellationToken);
            }
            catch (BookingConflictException)
            {
                // The one rule, asked rather than enforced. The caller gets a flag
                // to show; it is not an error, because nothing was attempted.
                conflict = true;
            }

            return new RentingQuoteDto
            {
                CarId = car.Id,
                Currency = price?.Currency ?? car.DailyRate?.Currency ?? settings.CurrencyCode,
                DailyRate = car.DailyRate is { } dailyRate ? MoneyDto.From(dailyRate) : null,
                BilledDays = billedDays,
                Price = price is null ? null : MoneyDto.From(price),
                CarStatus = car.Status,
                IsCarBookable = car.Status == CarStatus.Active,
                IsAvailable = !conflict,
            };
        }
    }
}
