using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Queries.GetCarFacetsQuery
{
    /// <summary>
    /// The counts beside the fleet list's filters. Takes the same filters the list
    /// does, because a facet is counted against the current narrowing (see
    /// CarQueryFilters) — the rail says what each option would show from here, not
    /// what it would show from an empty screen.
    /// </summary>
    [Authorize(Policy = Permissions.CarRead)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record GetCarFacetsQuery(
        string? Search = null,
        int? ModelId = null,
        string? Color = null,
        FuelType? FuelType = null,
        CarStatus? Status = null,
        int? BranchId = null,
        int? BrandId = null,
        bool? OnRent = null,
        DateTimeOffset? AddedFrom = null,
        DateTimeOffset? AddedTo = null
    ) : IRequest<CarFacetsDto>;

    public class GetCarFacetsQueryHandler : IRequestHandler<GetCarFacetsQuery, CarFacetsDto>
    {
        private readonly IApplicationDbContext _context;

        public GetCarFacetsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CarFacetsDto> Handle(GetCarFacetsQuery request, CancellationToken cancellationToken)
        {
            // One grouped read per dimension. They are separate queries rather than
            // one clever one because each leaves out a different filter, and the
            // fleet is a table an agency counts in hundreds.
            IQueryable<Domain.Entities.Car> Matching(string? except) =>
                _context.Cars
                    .AsNoTracking()
                    .ApplyCarFilters(
                        request.Search, request.ModelId, request.Color, request.FuelType,
                        request.Status, request.OnRent, request.BranchId, request.BrandId,
                        request.AddedFrom, request.AddedTo, except);

            var total = await Matching(null).CountAsync(cancellationToken);
            var fleet = await _context.Cars.AsNoTracking().CountAsync(cancellationToken);

            var statuses = await Matching(CarQueryFilters.StatusDimension)
                .GroupBy(c => c.Status)
                .OrderBy(g => g.Key)
                .Select(g => new CarStatusFacetDto { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            // Custody is one dimension with two answers, so both are counted over
            // the same set — and the set is the one that ignores the custody filter.
            var custody = Matching(CarQueryFilters.CustodyDimension);
            var custodyTotal = await custody.CountAsync(cancellationToken);
            var onRent = await custody.CountAsync(
                c => c.Rentings!.Any(r => r.RentingState == RentingState.InProgress), cancellationToken);

            var branches = await Matching(CarQueryFilters.BranchDimension)
                .GroupBy(c => new { c.BranchId, Name = c.Branch != null ? c.Branch.Name : null })
                .OrderBy(g => g.Key.Name)
                .Select(g => new CarNamedFacetDto { Id = g.Key.BranchId, Name = g.Key.Name, Count = g.Count() })
                .ToListAsync(cancellationToken);

            // Brands hang off the model; a car with no model joins in as a null
            // brand, which is the "not set" bucket rather than a dropped row.
            var brands = await Matching(CarQueryFilters.BrandDimension)
                .GroupBy(c => new
                {
                    Id = c.Model != null ? c.Model.BrandId : null,
                    Name = c.Model != null && c.Model.Brand != null ? c.Model.Brand.Name : null
                })
                .OrderBy(g => g.Key.Name)
                .Select(g => new CarNamedFacetDto { Id = g.Key.Id, Name = g.Key.Name, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var fuelTypes = await Matching(CarQueryFilters.FuelDimension)
                .GroupBy(c => c.FuelType)
                .OrderBy(g => g.Key)
                .Select(g => new CarFuelFacetDto { FuelType = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return new CarFacetsDto
            {
                Total = total,
                Fleet = fleet,
                Statuses = statuses,
                OnRent = onRent,
                InYard = custodyTotal - onRent,
                Branches = branches,
                Brands = brands,
                FuelTypes = fuelTypes
            };
        }
    }
}
