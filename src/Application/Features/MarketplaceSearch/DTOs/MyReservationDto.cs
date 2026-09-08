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

        // Shown when the booking was called off, whoever did it, and whether the
        // agency was the one who did — which is the only case the customer can
        // report (see CreateMyReportCommand).
        public string? CancelledReason { get; init; }
        public bool CancelledByAgency { get; init; }
        // Instant (recorded from the clock) — the SPA renders it local.
        public DateTime? CancelledAt { get; init; }
        // Whether the agency ever said yes to this booking. What separates a
        // promise from a request nobody answered, and so what decides both the
        // agency's reliability and whether this can be reported.
        public bool WasConfirmed { get; init; }

        // The complaint the customer already raised about this booking, if any,
        // and whether they may still raise one. Both decided by the server so the
        // rule lives in one place.
        public int? MyReportId { get; set; }
        public AgencyReportStatus? MyReportStatus { get; set; }
        public bool CanReport { get; set; }

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
                      // The agency broke a promise it had made: a request it
                      // simply never answered is not this.
                      .Map(d => d.CancelledByAgency,
                           src => src.Status == ReservationStatus.Cancelled
                                  && src.CancelledAfterConfirmation
                                  && !src.CancelledByCustomer)
                      // Repeats AgencyReport.CanReport inline, because EF has to
                      // translate the test into SQL. Change the rule there and
                      // this changes with it.
                      .Map(d => d.WasConfirmed,
                           src => src.Status == ReservationStatus.Confirmed
                                  || src.Status == ReservationStatus.Paid
                                  || src.Status == ReservationStatus.Converted
                                  || (src.Status == ReservationStatus.Cancelled
                                      && src.CancelledAfterConfirmation))
                      .Map(d => d.CarModelName,
                           src => src.Car != null && src.Car.Model != null ? src.Car.Model.Name : null)
                      .Map(d => d.CarBrandName,
                           src => src.Car != null && src.Car.Model != null && src.Car.Model.Brand != null
                               ? src.Car.Model.Brand.Name : null);
            }
        }
    }
}
