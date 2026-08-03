using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.DTOs
{
    public class RentingDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        // Optimistic-concurrency token; echoed back on update (see P.8).
        public byte[]? RowVersion { get; init; }
        public int? CarId { get; init; }
        public string? CarMatricule { get; init; }
        public string? CarModelName { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public int? SecondClientId { get; init; }
        public string? SecondClientName { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public int? StartMileage { get; init; }
        public int? EndMileage { get; init; }
        // Snapshot price agreed at creation (see IPricingService).
        public MoneyDto? Price { get; init; }
        // Net collected against this renting — refunds and reversals are negative
        // entries, so a plain sum is the true figure. Price − Paid is what is
        // still owed, and it is the same ceiling CreatePaymentCommand enforces
        // (extra services are billed separately and are not in it), so a caller
        // can tell whether there is anything left to collect without reading the
        // ledger. Null when the renting carries no price.
        public MoneyDto? Paid { get; init; }
        public MoneyDto? Outstanding { get; init; }
        public RentingState RentingState { get; init; }
        public string? Notes { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Renting, RentingDto>()
                      .Map(dest => dest.CarMatricule, src => src.Car != null ? src.Car.Matricule : null)
                      .Map(dest => dest.CarModelName,
                           src => src.Car != null && src.Car.Model != null ? src.Car.Model.Name : null)
                      .Map(dest => dest.ClientName,
                           src => src.Client != null ? src.Client.FirstName + " " + src.Client.LastName : null)
                      .Map(dest => dest.SecondClientName,
                           src => src.SecondClient != null ? src.SecondClient.FirstName + " " + src.SecondClient.LastName : null)
                      // Projected in SQL over the owned amount columns, like the
                      // credits screen's sums; an optional owned reference is read
                      // through a null check rather than dereferenced.
                      .Map(dest => dest.Paid,
                           src => src.Price == null
                               ? null
                               : new MoneyDto(
                                   src.Payments!.Where(p => p.PayementAmount != null)
                                                .Sum(p => p.PayementAmount!.Amount),
                                   src.Price.Currency))
                      .Map(dest => dest.Outstanding,
                           src => src.Price == null
                               ? null
                               : new MoneyDto(
                                   src.Price.Amount
                                   - src.Payments!.Where(p => p.PayementAmount != null)
                                                  .Sum(p => p.PayementAmount!.Amount),
                                   src.Price.Currency));
            }
        }
    }
}
