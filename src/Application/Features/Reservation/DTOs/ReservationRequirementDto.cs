using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Reservation.DTOs
{
    /// <summary>
    /// One thing the agency asks for before pickup, as both sides see it. The
    /// same shape serves the agency screen and the customer's own checklist —
    /// there is nothing on a requirement the customer may not see, since it is
    /// addressed to them.
    /// </summary>
    public class ReservationRequirementDto
    {
        public int Id { get; init; }
        public int ReservationId { get; init; }
        public ReservationRequirementKind Kind { get; init; }
        public string Label { get; init; } = string.Empty;
        public MoneyDto? Amount { get; init; }
        public PaymentMethod? ExpectedMethod { get; init; }
        public ReservationRequirementStatus Status { get; init; }
        public string? SubmittedFileUrl { get; init; }
        public string? SubmittedNote { get; init; }
        // Instants (recorded from the clock), so the SPA renders them local.
        public DateTime? SubmittedAt { get; init; }
        public DateTime? ReviewedAt { get; init; }
        // Why it was refused, or why the agency dropped it. Shown to the customer.
        public string? ReviewNote { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.ReservationRequirement, ReservationRequirementDto>()
                      .Map(dest => dest.SubmittedFileUrl,
                           src => src.SubmittedFile != null ? src.SubmittedFile.Url : null);
            }
        }
    }
}
