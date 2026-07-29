using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch
{
    // The single definition of "this car is on the public marketplace", shared by
    // the search, the map, the destination picker, the agency shopfront and the
    // home-page showcase so they can never disagree about what is on offer.
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

        // The "where / what / who" half of a public search. Shared by the paged
        // result list and the map so a pin can never stand for a car the list
        // would have filtered out.
        public static IQueryable<Domain.Entities.Car> Matching(
            this IQueryable<Domain.Entities.Car> cars,
            int? countryId, int? branchId, int? brandId, int? agencyId)
        {
            if (branchId is int branch)
                cars = cars.Where(c => c.BranchId == branch);

            // Same branch-else-agency rule the destination picker counts by, so a
            // country that lists N cars does not then return fewer.
            if (countryId is int country)
                cars = cars.InCountry(country);

            if (brandId is int brand)
                cars = cars.Where(c => c.Model != null && c.Model.BrandId == brand);

            if (agencyId is int agency)
                cars = cars.Where(c => c.AgencyId == agency);

            return cars;
        }

        // Available = no blocking renting (non-terminal) and no active
        // reservation overlapping the half-open [start, end). Both checks are
        // written as correlated sub-queries with their OWN IgnoreQueryFilters:
        // the customer has no tenant, so a sub-query that re-applied the tenant
        // filter would match nothing and wrongly show a booked car. (Relying on
        // the root's IgnoreQueryFilters to propagate through the Car.Rentings
        // navigation is not something to depend on.)
        public static IQueryable<Domain.Entities.Car> AvailableBetween(
            this IQueryable<Domain.Entities.Car> cars,
            IApplicationDbContext context, DateTime start, DateTime end)
        {
            cars = cars.Where(c => !context.Rentings.IgnoreQueryFilters().Any(r =>
                r.CarId == c.Id
                && r.RentingState != RentingState.Done
                && r.RentingState != RentingState.Cancelled
                && r.StartDate < end
                && r.EndDate > start));

            return cars.Where(c => !context.Reservations.IgnoreQueryFilters().Any(r =>
                r.CarId == c.Id
                && (r.Status == ReservationStatus.PendingConfirmation
                    || r.Status == ReservationStatus.Confirmed
                    || r.Status == ReservationStatus.Paid)
                && r.StartDate < end
                && r.EndDate > start));
        }

        // Viewport filter for the map: keep the cars whose pick-up point falls
        // inside the rectangle the visitor is looking at. Geography stores the
        // point as (longitude, latitude) — X is the longitude, Y the latitude,
        // which EF translates to the .Long / .Lat SQL Server accessors.
        //
        // A box that crosses the antimeridian (west > east) is not handled: the
        // marketplace is regional, and a wrong-way box would silently return
        // nothing, so it is treated as an unusable filter and ignored.
        public static IQueryable<Domain.Entities.Car> WithinBounds(
            this IQueryable<Domain.Entities.Car> cars,
            double? south, double? west, double? north, double? east)
        {
            if (south is not double s || west is not double w
                || north is not double n || east is not double e || w > e)
            {
                return cars;
            }

            return cars.Where(c => c.Branch != null
                                   && c.Branch.Location != null
                                   && c.Branch.Location.Y >= s
                                   && c.Branch.Location.Y <= n
                                   && c.Branch.Location.X >= w
                                   && c.Branch.Location.X <= e);
        }
    }
}
