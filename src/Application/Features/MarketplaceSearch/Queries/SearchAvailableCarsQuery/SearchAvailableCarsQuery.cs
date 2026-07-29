using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery
{
    // Public, cross-agency search: no [Authorize]/[RequiresFeature] and no tenant.
    // This is one of the two sanctioned IgnoreQueryFilters locations (see
    // TenantEnforcementTests); it MUST live under Features/MarketplaceSearch/.
    //
    // South/West/North/East are the map viewport, sent by the "search as I move
    // the map" mode so the list and the pins always describe the same set. They
    // are optional: the list-only view sends none and searches everywhere.
    public record SearchAvailableCarsQuery(
        DateTime StartDate,
        DateTime EndDate,
        int? CountryId = null,
        int? BranchId = null,
        int? BrandId = null,
        int? AgencyId = null,
        double? South = null,
        double? West = null,
        double? North = null,
        double? East = null,
        int PageNumber = 1,
        int PageSize = 12
    ) : IRequest<PaginatedList<MarketplaceCarDto>>;

    public class SearchAvailableCarsQueryHandler
        : IRequestHandler<SearchAvailableCarsQuery, PaginatedList<MarketplaceCarDto>>
    {
        private readonly IApplicationDbContext _context;

        public SearchAvailableCarsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<MarketplaceCarDto>> Handle(
            SearchAvailableCarsQuery request, CancellationToken cancellationToken)
        {
            // What "on offer", "matches the filters" and "is free for the window"
            // mean all live in MarketplaceCars — the same three steps the map
            // query runs, so a pin and a result card can never disagree.
            var query = MarketplaceCars.Offered(_context)
                .Matching(request.CountryId, request.BranchId, request.BrandId, request.AgencyId)
                .WithinBounds(request.South, request.West, request.North, request.East)
                .AvailableBetween(_context, request.StartDate, request.EndDate);

            return await query
                .OrderBy(c => c.DailyRate!.Amount)
                .ProjectToType<MarketplaceCarDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery
{
    public class SearchAvailableCarsQueryValidator : AbstractValidator<SearchAvailableCarsQuery>
    {
        public SearchAvailableCarsQueryValidator(ILocalizer localizer)
        {
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);
            RuleFor(v => v.PageSize).InclusiveBetween(1, 50);

            // A viewport out of range is a bug in the caller, not a search with
            // no results — say so rather than quietly returning an empty page.
            RuleFor(v => v.South).InclusiveBetween(-90, 90).When(v => v.South.HasValue);
            RuleFor(v => v.North).InclusiveBetween(-90, 90).When(v => v.North.HasValue);
            RuleFor(v => v.West).InclusiveBetween(-180, 180).When(v => v.West.HasValue);
            RuleFor(v => v.East).InclusiveBetween(-180, 180).When(v => v.East.HasValue);
        }
    }
}
