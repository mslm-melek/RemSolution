using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.SearchCarsMapQuery
{
    // The map half of the public search: the same cars the result list shows,
    // reduced to one pin per pick-up place. Public and cross-agency like the
    // list, and it MUST stay under Features/MarketplaceSearch/ for the tenant
    // bypass it inherits (see TenantEnforcementTests).
    //
    // Not paginated. The list is paged because a visitor reads it card by card;
    // the map is read as a whole — a viewport showing 3 of its 40 pins would be
    // a lie about where the cars are — so it returns every place in the box, up
    // to a hard cap.
    public record SearchCarsMapQuery(
        DateTime StartDate,
        DateTime EndDate,
        int? CountryId = null,
        int? BranchId = null,
        int? BrandId = null,
        int? AgencyId = null,
        double? South = null,
        double? West = null,
        double? North = null,
        double? East = null
    ) : IRequest<IList<MarketplaceMapPointDto>>;

    public class SearchCarsMapQueryHandler
        : IRequestHandler<SearchCarsMapQuery, IList<MarketplaceMapPointDto>>
    {
        // A zoomed-out map of a whole country is the realistic worst case. Past
        // this many places the pins are unreadable anyway, and the cap keeps one
        // careless viewport from pulling the entire branch table.
        private const int MaxPoints = 300;

        private readonly IApplicationDbContext _context;

        public SearchCarsMapQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<MarketplaceMapPointDto>> Handle(
            SearchCarsMapQuery request, CancellationToken cancellationToken)
        {
            // Exactly the pipeline SearchAvailableCarsQuery runs, plus the
            // requirement that the car can actually be placed: a branchless or
            // un-geocoded car is listable but has no pin.
            var cars = MarketplaceCars.Offered(_context)
                .Matching(request.CountryId, request.BranchId, request.BrandId, request.AgencyId)
                .WithinBounds(request.South, request.West, request.North, request.East)
                .AvailableBetween(_context, request.StartDate, request.EndDate)
                .Where(c => c.Branch != null && c.Branch.Location != null);

            // Grouped in SQL: one row per place, carrying the cheapest rate as
            // amount + currency so the pin label keeps its currency. The currency
            // is part of the key rather than an aggregate — an agency prices its
            // whole fleet in one currency (it comes from AgencySettings), so this
            // never splits a place in two, and MIN over mixed currencies would be
            // meaningless if it ever did.
            var places = await cars
                .GroupBy(c => new
                {
                    BranchId = c.Branch!.Id,
                    BranchName = c.Branch.Name,
                    c.AgencyId,
                    AgencyName = c.Agency!.Name,
                    Latitude = c.Branch.Location!.Y,
                    Longitude = c.Branch.Location.X,
                    Currency = c.DailyRate!.Currency,
                })
                .Select(g => new
                {
                    g.Key,
                    CarCount = g.Count(),
                    FromAmount = g.Min(c => c.DailyRate!.Amount),
                })
                // Busiest places first, so the cap (if it ever bites) drops the
                // pins that matter least.
                .OrderByDescending(g => g.CarCount)
                .Take(MaxPoints)
                .ToListAsync(cancellationToken);

            if (places.Count == 0)
            {
                return new List<MarketplaceMapPointDto>();
            }

            // Ratings are read once per agency rather than as a correlated
            // aggregate on every grouped row: an agency has one reputation, and
            // several of its places usually appear in the same viewport.
            var agencyIds = places.Select(p => p.Key.AgencyId).Distinct().ToList();

            var ratings = await _context.AgencyReviews
                .AsNoTracking()
                .Where(r => agencyIds.Contains(r.AgencyId))
                .GroupBy(r => r.AgencyId)
                .Select(g => new { AgencyId = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
                .ToDictionaryAsync(x => x.AgencyId, x => x, cancellationToken);

            return places
                .Select(p =>
                {
                    ratings.TryGetValue(p.Key.AgencyId, out var rating);

                    return new MarketplaceMapPointDto
                    {
                        BranchId = p.Key.BranchId,
                        BranchName = p.Key.BranchName,
                        AgencyId = p.Key.AgencyId,
                        AgencyName = p.Key.AgencyName,
                        Latitude = p.Key.Latitude,
                        Longitude = p.Key.Longitude,
                        CarCount = p.CarCount,
                        FromDailyRate = new MoneyDto(p.FromAmount, p.Key.Currency),
                        AgencyRating = rating?.Average,
                        AgencyReviewCount = rating?.Count ?? 0,
                    };
                })
                .ToList();
        }
    }
}

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.SearchCarsMapQuery
{
    public class SearchCarsMapQueryValidator : AbstractValidator<SearchCarsMapQuery>
    {
        public SearchCarsMapQueryValidator(ILocalizer localizer)
        {
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);

            RuleFor(v => v.South).InclusiveBetween(-90, 90).When(v => v.South.HasValue);
            RuleFor(v => v.North).InclusiveBetween(-90, 90).When(v => v.North.HasValue);
            RuleFor(v => v.West).InclusiveBetween(-180, 180).When(v => v.West.HasValue);
            RuleFor(v => v.East).InclusiveBetween(-180, 180).When(v => v.East.HasValue);
        }
    }
}
