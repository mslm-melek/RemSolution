using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Payment.DTOs
{
    public class PaymentDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public int? RentingId { get; init; }
        public DateTime? PayementDate { get; init; }
        public MoneyDto? PayementAmount { get; init; }
        public PaymentMethod Method { get; init; }
        public string? Notes { get; init; }
        // Set on a reversal entry: the payment it offsets.
        public int? ReversesPaymentId { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Payment, PaymentDto>()
                      .Map(dest => dest.ClientName,
                           src => src.Client != null ? src.Client.FirstName + " " + src.Client.LastName : null);
            }
        }
    }
}
