namespace RemSolution.Application.Features.ExtraServicesType.DTOs
{
    // Plain fields — Mapster maps by member name, no explicit mapping needed.
    public class ExtraServicesTypeDto
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public decimal? Amount { get; init; }
        public bool IsActive { get; init; }
    }
}
