using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    /// <summary>
    /// How the fleet divides up, for the counts in the list's filter rail.
    /// <para>
    /// Each number answers "how many cars would this option show?", so it is
    /// counted with every other filter applied but not its own (see
    /// CarQueryFilters): the rail stays usable after a click instead of collapsing
    /// to one non-zero row.
    /// </para>
    /// Options with nothing in them are absent rather than zero — a brand the
    /// agency no longer keeps is not a filter worth drawing.
    /// </summary>
    public class CarFacetsDto
    {
        /// <summary>Cars matching everything currently selected — the list's own total.</summary>
        public int Total { get; init; }

        /// <summary>The whole fleet, ignoring every filter: what "clear" would show.</summary>
        public int Fleet { get; init; }

        public IList<CarStatusFacetDto> Statuses { get; init; } = new List<CarStatusFacetDto>();

        /// <summary>
        /// Custody, which is not the administrative status: how many cars have a
        /// hire running right now, and how many are standing on the forecourt.
        /// </summary>
        public int OnRent { get; init; }
        public int InYard { get; init; }

        /// <summary>Branches, and the cars based nowhere (Id null).</summary>
        public IList<CarNamedFacetDto> Branches { get; init; } = new List<CarNamedFacetDto>();

        /// <summary>Brands, read through each car's model.</summary>
        public IList<CarNamedFacetDto> Brands { get; init; } = new List<CarNamedFacetDto>();

        public IList<CarFuelFacetDto> FuelTypes { get; init; } = new List<CarFuelFacetDto>();
    }

    public class CarStatusFacetDto
    {
        public CarStatus Status { get; init; }
        public int Count { get; init; }
    }

    /// <summary>A branch or a brand: an id to filter by, a name to show. Id null is
    /// the "not set" bucket, which is a real answer for both.</summary>
    public class CarNamedFacetDto
    {
        public int? Id { get; init; }
        public string? Name { get; init; }
        public int Count { get; init; }
    }

    public class CarFuelFacetDto
    {
        /// <summary>Null for cars whose fuel nobody recorded.</summary>
        public FuelType? FuelType { get; init; }
        public int Count { get; init; }
    }
}
