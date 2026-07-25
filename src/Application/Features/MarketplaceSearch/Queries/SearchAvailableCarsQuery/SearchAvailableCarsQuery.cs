using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery
{
    // Public, cross-agency search: no [Authorize]/[RequiresFeature] and no tenant.
    // This is one of the two sanctioned IgnoreQueryFilters locations (see
    // TenantEnforcementTests); it MUST live under Features/MarketplaceSearch/.
    public record SearchAvailableCarsQuery(
        DateTime StartDate,
        DateTime EndDate,
        int? CountryId = null,
        int? BranchId = null,
        int? BrandId = null,
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
            var start = request.StartDate;
            var end = request.EndDate;

            // IgnoreQueryFilters drops BOTH the AgencyId and the !IsDeleted global
            // filters, so soft-delete is re-applied here explicitly.
            var query = _context.Cars
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                            && c.Status == CarStatus.Active
                            && c.DailyRate != null);

            if (request.BranchId is int branchId)
                query = query.Where(c => c.BranchId == branchId);

            if (request.CountryId is int countryId)
                query = query.Where(c => c.Branch != null && c.Branch.CountryId == countryId);

            if (request.BrandId is int brandId)
                query = query.Where(c => c.Model != null && c.Model.BrandId == brandId);

            // Available = no blocking renting (non-terminal) and no active
            // reservation overlapping the half-open [start, end). Both checks are
            // written as correlated sub-queries with their OWN IgnoreQueryFilters:
            // the customer has no tenant, so a sub-query that re-applied the tenant
            // filter would match nothing and wrongly show a booked car. (Relying on
            // the root's IgnoreQueryFilters to propagate through the Car.Rentings
            // navigation is not something to depend on.)
            query = query.Where(c => !_context.Rentings.IgnoreQueryFilters().Any(r =>
                r.CarId == c.Id
                && r.RentingState != RentingState.Done
                && r.RentingState != RentingState.Cancelled
                && r.StartDate < end
                && r.EndDate > start));

            query = query.Where(c => !_context.Reservations.IgnoreQueryFilters().Any(r =>
                r.CarId == c.Id
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                && r.StartDate < end
                && r.EndDate > start));

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
        public SearchAvailableCarsQueryValidator()
        {
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage("The end date must be after the start date.");
            RuleFor(v => v.PageSize).InclusiveBetween(1, 50);
        }
    }
}
