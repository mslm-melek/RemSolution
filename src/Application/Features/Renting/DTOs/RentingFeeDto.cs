using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.DTOs
{
    /// <summary>
    /// One extra charge on a hire (see Domain.Entities.RentingFee). The kind is
    /// sent as the enum, not as a label: the screens and the invoice each render
    /// it in their own language.
    /// </summary>
    public class RentingFeeDto
    {
        public int Id { get; init; }
        public int RentingId { get; init; }
        public RentingFeeKind Kind { get; init; }
        public MoneyDto? Amount { get; init; }
        public string? Note { get; init; }

        /// <summary>When it was booked — normally the day the car came back.</summary>
        public DateTimeOffset? CreatedOn { get; init; }
    }
}
