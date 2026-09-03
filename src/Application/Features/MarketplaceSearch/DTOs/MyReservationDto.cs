using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // A customer's reservation, viewed across agencies.
    public class MyReservationDto
    {
        public int Id { get; init; }
        // Which agency's terms apply to it — the cancellation policy is theirs.
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public string? CarBrandName { get; init; }
        public string? CarModelName { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public MoneyDto? Price { get; init; }
        public ReservationStatus Status { get; init; }
        public DateTime? ExpiresAt { get; init; }
        // Shown to the customer when the agency declined the request.
        public string? RejectedReason { get; init; }

        // What calling it off actually costs, told before the decision and not
        // after it. CancellationFee is what WAS charged on a booking already
        // cancelled; the two below are the live answer for one that is not.
        public MoneyDto? CancellationFee { get; init; }
        public bool CanCancel { get; set; }
        public MoneyDto? CancellationFeeIfCancelledNow { get; set; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Reservation, MyReservationDto>()
                      .Map(d => d.AgencyName, src => src.Agency != null ? src.Agency.Name : null)
                      .Map(d => d.CarModelName,
                           src => src.Car != null && src.Car.Model != null ? src.Car.Model.Name : null)
                      .Map(d => d.CarBrandName,
                           src => src.Car != null && src.Car.Model != null && src.Car.Model.Brand != null
                               ? src.Car.Model.Brand.Name : null);
            }
        }
    }
}
