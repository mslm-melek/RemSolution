using RemSolution.Domain.Enums;
using RemSolution.Domain.Events;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A hold on a specific car for a date range, and the aggregate that owns the
    /// reservation lifecycle (see <see cref="ReservationStatus"/>). Data fields
    /// (car, client, dates, prices, notes) stay settable so the edit/re-price
    /// flow can mutate a pending hold, but <see cref="Status"/> and the reason
    /// fields change ONLY through the guarded transition methods below, each of
    /// which raises a domain event. Build a new hold with <see cref="Create"/>,
    /// never <c>new Reservation { Status = ... }</c>.
    /// </summary>
    public class Reservation : BaseAuditableEntity, ITenantEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        // The car being held. Nullable and SetNull on car delete, mirroring
        // Renting — a reservation anchors availability on a specific car.
        public int? CarId { get; set; }
        public virtual Car? Car { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        // Snapshot price for the held period (from IPricingService), the running
        // total already paid against it, and any deposit collected.
        public Money? Price { get; set; }
        public Money? PayedPrice { get; set; }
        public Money? DepositAmount { get; set; }
        public string? Notes { get; set; }

        // Lifecycle of the hold — mutated only via the methods below.
        public ReservationStatus Status { get; private set; } = ReservationStatus.PendingConfirmation;

        // When a pending hold lapses. Set from AgencySettings.ReservationExpiryHours
        // at creation; the reservation-expiry job sweeps holds past this instant.
        public DateTime? ExpiresAt { get; set; }

        // Why the hold left the happy path. RejectedReason is shown to the client
        // (why the agency declined); the others feed later analytics.
        public string? RejectedReason { get; private set; }
        public string? CancelledReason { get; private set; }
        public string? ExpiredReason { get; private set; }

        // Set when the hold is converted into an actual renting.
        public int? RentingId { get; private set; }
        public virtual Renting? Renting { get; private set; }

        // EF materialisation constructor.
        private Reservation() { }

        /// <summary>Factory for a new pending hold. AgencyId is stamped on save.</summary>
        public static Reservation Create(
            int carId, DateTime startDate, DateTime endDate,
            Money? price, DateTime expiresAt,
            int? clientId = null, Money? payedPrice = null,
            Money? depositAmount = null, string? notes = null)
        {
            return new Reservation
            {
                CarId = carId,
                ClientId = clientId,
                StartDate = startDate,
                EndDate = endDate,
                Price = price,
                PayedPrice = payedPrice,
                DepositAmount = depositAmount,
                Notes = notes,
                ExpiresAt = expiresAt,
                Status = ReservationStatus.PendingConfirmation,
            };
        }

        /// <summary>Agency approves the hold. Pending → Confirmed.</summary>
        public void Confirm()
        {
            Require(ReservationStatus.PendingConfirmation, "confirmed");
            Status = ReservationStatus.Confirmed;
            AddDomainEvent(new ReservationConfirmedEvent(this));
        }

        /// <summary>Agency declines the hold, telling the client why. Pending → Rejected.</summary>
        public void Reject(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A rejection reason is required.", nameof(reason));

            Require(ReservationStatus.PendingConfirmation, "rejected");
            Status = ReservationStatus.Rejected;
            RejectedReason = reason;
            AddDomainEvent(new ReservationRejectedEvent(this, reason));
        }

        /// <summary>Cancel an active hold. Allowed while Pending/Confirmed/Paid.</summary>
        public void Cancel(string? reason)
        {
            if (Status is not (ReservationStatus.PendingConfirmation
                or ReservationStatus.Confirmed or ReservationStatus.Paid))
            {
                throw new InvalidReservationTransitionException(Status, "cancelled");
            }

            Status = ReservationStatus.Cancelled;
            CancelledReason = reason;
            AddDomainEvent(new ReservationCancelledEvent(this, reason));
        }

        /// <summary>Background sweep lapses an unconfirmed hold. Pending → Expired.</summary>
        public void Expire(string? reason = null)
        {
            Require(ReservationStatus.PendingConfirmation, "expired");
            Status = ReservationStatus.Expired;
            ExpiredReason = reason ?? "Not confirmed before the hold expired.";
            AddDomainEvent(new ReservationExpiredEvent(this));
        }

        /// <summary>Full settlement recorded. Confirmed → Paid.</summary>
        public void MarkPaid()
        {
            Require(ReservationStatus.Confirmed, "marked paid");
            Status = ReservationStatus.Paid;
            AddDomainEvent(new ReservationPaidEvent(this));
        }

        /// <summary>Convert into a renting. Allowed from Confirmed/Paid.</summary>
        public void Convert(Renting renting)
        {
            if (Status is not (ReservationStatus.Confirmed or ReservationStatus.Paid))
            {
                throw new InvalidReservationTransitionException(Status, "converted into a renting");
            }

            Renting = renting;
            Status = ReservationStatus.Converted;
            AddDomainEvent(new ReservationConvertedEvent(this, renting));
        }

        private void Require(ReservationStatus expected, string action)
        {
            if (Status != expected)
                throw new InvalidReservationTransitionException(Status, action);
        }
    }
}
