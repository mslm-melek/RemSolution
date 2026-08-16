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

        /// <summary>
        /// The car's own odometer (see Car.Mileage), so a row can offer the pickup
        /// reading when the hire is started from the list — the same figure the
        /// booking wizard offers, without a second round-trip per row.
        /// </summary>
        public int? CarMileage { get; init; }
        // Snapshot price agreed at creation (see IPricingService).
        public MoneyDto? Price { get; init; }

        /// <summary>
        /// Kept by the agency for calling a hire off (see Renting.CancellationFee)
        /// — null on a live hire, and null on one cancelled for free. It replaces
        /// the price as what a cancelled hire charges, so a screen showing money
        /// needs it to explain why the outstanding figure is what it is.
        /// </summary>
        public MoneyDto? CancellationFee { get; init; }

        /// <summary>
        /// What the return turned out to owe on top of the price — late days,
        /// damage, kilometres over the allowance (see Renting.AddFee). Zero, not
        /// null, when there are none: a row showing money needs a figure to add.
        /// </summary>
        public MoneyDto? Fees { get; init; }

        // Net collected against this renting — refunds and reversals are negative
        // entries, so a plain sum is the true figure. Charged − Paid is what is
        // still owed, and it is the same ceiling CreatePaymentCommand enforces
        // (extra services are billed separately and are not in it, return fees
        // are), so a caller can tell whether there is anything left to collect
        // without reading the ledger. Null when the renting carries neither a
        // price nor a fee — there is nothing to be owed on.
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
                      .Map(dest => dest.CarMileage, src => src.Car != null ? src.Car.Mileage : null)
                      .Map(dest => dest.ClientName,
                           src => src.Client != null ? src.Client.FirstName + " " + src.Client.LastName : null)
                      .Map(dest => dest.SecondClientName,
                           src => src.SecondClient != null ? src.SecondClient.FirstName + " " + src.SecondClient.LastName : null)
                      // Projected in SQL over the owned amount columns, like the
                      // credits screen's sums; an optional owned reference is read
                      // through a null check rather than dereferenced.
                      //
                      // The three money figures below share one shape: null only
                      // when the hire carries neither a price nor a charge, and
                      // otherwise stated in the hire's currency — the first fee's
                      // when there is no price to take it from (a courtesy car
                      // returned damaged is priceless but not free).
                      .Map(dest => dest.Fees,
                           src => src.Price == null && !src.Fees.Any()
                               ? null
                               : new MoneyDto(
                                   src.Fees.Sum(f => f.Amount == null ? 0m : f.Amount.Amount),
                                   src.Price != null
                                       ? src.Price.Currency
                                       : src.Fees.Select(f => f.Amount!.Currency).FirstOrDefault()!))
                      .Map(dest => dest.Paid,
                           src => src.Price == null && !src.Fees.Any()
                               ? null
                               : new MoneyDto(
                                   src.Payments!.Where(p => p.PayementAmount != null)
                                                .Sum(p => p.PayementAmount!.Amount),
                                   src.Price != null
                                       ? src.Price.Currency
                                       : src.Fees.Select(f => f.Amount!.Currency).FirstOrDefault()!))
                      // A cancelled hire is owed its cancellation fee, not its
                      // price, and either way it is owed the charges established
                      // on it — the same charge rule the client's balance uses
                      // (see ClientCreditRows). So a hire cancelled for free shows
                      // nothing outstanding even when it was never paid, and one
                      // cancelled with a fee shows exactly that fee less what has
                      // been collected against it.
                      .Map(dest => dest.Outstanding,
                           src => src.Price == null && !src.Fees.Any()
                               ? null
                               : new MoneyDto(
                                   (src.RentingState == RentingState.Cancelled
                                       ? (src.CancellationFee == null ? 0m : src.CancellationFee.Amount)
                                       : (src.Price == null ? 0m : src.Price.Amount))
                                   + src.Fees.Sum(f => f.Amount == null ? 0m : f.Amount.Amount)
                                   - src.Payments!.Where(p => p.PayementAmount != null)
                                                  .Sum(p => p.PayementAmount!.Amount),
                                   src.Price != null
                                       ? src.Price.Currency
                                       : src.Fees.Select(f => f.Amount!.Currency).FirstOrDefault()!));
            }
        }
    }
}
