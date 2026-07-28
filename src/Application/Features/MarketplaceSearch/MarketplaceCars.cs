using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch
{
    // The single definition of "this car is on the public marketplace", shared by
    // the search, the destination picker, the agency shopfront and the home-page
    // showcase so the four can never disagree about what is on offer.
    internal static class MarketplaceCars
    {
        // Offered = not archived, bookable and priced. IgnoreQueryFilters drops
        // BOTH the AgencyId and the !IsDeleted global filters (the visitor has no
        // tenant), so soft-delete is re-applied explicitly. This is one of the two
        // sanctioned bypass locations — see TenantEnforcementTests.
        public static IQueryable<Domain.Entities.Car> Offered(IApplicationDbContext context) =>
            context.Cars
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                            && c.Status == CarStatus.Active
                            && c.DailyRate != null);

        // A car's country is its branch's, falling back to its agency's: BranchId
        // is nullable, and a car with no branch would otherwise be unreachable
        // from the country picker even though the unfiltered search lists it.
        public static IQueryable<Domain.Entities.Car> InCountry(this IQueryable<Domain.Entities.Car> cars, int countryId) =>
            cars.Where(c => c.Branch != null
                ? c.Branch.CountryId == countryId
                : c.Agency != null && c.Agency.CountryId == countryId);
    }
}
