using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Queries
{
    /// <summary>
    /// The fleet list's filters, written once because two queries ask them: the
    /// list itself (see GetCarsWithPaginationQuery) and the counts beside it (see
    /// GetCarFacetsQuery). Keeping them here is what stops the numbers in the
    /// filter rail from drifting away from the rows they promise.
    /// </summary>
    internal static class CarQueryFilters
    {
        // The dimensions a facet can stand for. A facet counts the rows its option
        // WOULD show, so it applies every filter except the one it belongs to —
        // otherwise picking "Maintenance" would leave every other status reading 0
        // and the rail would become a dead end.
        public const string StatusDimension = "status";
        public const string CustodyDimension = "custody";
        public const string BranchDimension = "branch";
        public const string BrandDimension = "brand";
        public const string FuelDimension = "fuel";

        public static IQueryable<Domain.Entities.Car> ApplyCarFilters(
            this IQueryable<Domain.Entities.Car> query,
            string? search,
            int? modelId,
            string? color,
            FuelType? fuelType,
            CarStatus? status,
            bool? onRent,
            int? branchId,
            int? brandId,
            DateTimeOffset? addedFrom,
            DateTimeOffset? addedTo,
            string? except = null)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                // A local, because EF cannot translate a captured nullable
                // parameter's Value inside the predicate.
                var term = search;
                query = query.Where(c =>
                    (c.Matricule != null && c.Matricule.Contains(term)) ||
                    (c.Model != null && c.Model.Name != null && c.Model.Name.Contains(term)));
            }

            if (modelId.HasValue)
                query = query.Where(c => c.ModelId == modelId);

            if (brandId.HasValue && except != BrandDimension)
                query = query.Where(c => c.Model != null && c.Model.BrandId == brandId);

            if (!string.IsNullOrWhiteSpace(color))
                query = query.Where(c => c.Color == color);

            if (fuelType.HasValue && except != FuelDimension)
                query = query.Where(c => c.FuelType == fuelType);

            if (status.HasValue && except != StatusDimension)
                query = query.Where(c => c.Status == status);

            if (branchId.HasValue && except != BranchDimension)
                query = query.Where(c => c.BranchId == branchId);

            if (onRent.HasValue && except != CustodyDimension)
                query = onRent.Value
                    ? query.Where(c => c.Rentings!.Any(r => r.RentingState == RentingState.InProgress))
                    : query.Where(c => !c.Rentings!.Any(r => r.RentingState == RentingState.InProgress));

            // Half-open [AddedFrom, AddedTo) over when the car was recorded, which
            // is the only "added on" the model has.
            if (addedFrom.HasValue)
                query = query.Where(c => c.CreatedOn >= addedFrom);

            if (addedTo.HasValue)
                query = query.Where(c => c.CreatedOn < addedTo);

            return query;
        }
    }
}
