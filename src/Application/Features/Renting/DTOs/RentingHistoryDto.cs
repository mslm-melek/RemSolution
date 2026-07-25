using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.DTOs
{
    // Read shape of a RentingHistory snapshot. Plain fields — Mapster maps by
    // member name (Money → MoneyDto included), so no explicit mapping is needed.
    public class RentingHistoryDto
    {
        public int Id { get; init; }
        public int? RentingId { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public int? StartMileage { get; init; }
        public int? EndMileage { get; init; }
        public MoneyDto? Price { get; init; }
        public RentingState RentingState { get; init; }
    }
}
