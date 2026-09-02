using RemSolution.Domain.Enums;
using RemSolution.Domain.Events;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A hire, and the aggregate that owns its lifecycle. Handlers load, call one
    /// method, and save — they never assign fields, which is what keeps the
    /// invariants true for every entry point.
    /// </summary>
    public class Renting : BaseAuditableEntity, ITenantEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        // Stamped by the tenant interceptor, not by the methods below.
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        // Nullable and SetNull on delete: losing the car does not delete the
        // record of having hired it out.
        public int? CarId { get; private set; }
        public virtual Car? Car { get; private set; }
        public int? ClientId { get; private set; }
        public virtual Client? Client { get; private set; }
        public int? SecondClientId { get; private set; }
        public virtual Client? SecondClient { get; private set; }
        public DateTime? StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }
        public int? StartMileage { get; private set; }
        public int? EndMileage { get; private set; }
        // Snapshotted at creation, never re-read from the car afterwards.
        public Money? Price { get; private set; }
        // Refundable deposit, carried over from the reservation on conversion.
        public Money? DepositAmount { get; private set; }

        /// <summary>
        /// How much of <see cref="DepositAmount"/> the agency kept, and when that
        /// was decided. Both null while the question is still open — which is the
        /// point of storing them: a deposit whose fate nobody recorded is the
        /// dispute that surfaces three weeks later with nothing to arbitrate it,
        /// so an unsettled deposit stays visible instead of quietly lapsing.
        /// <para>
        /// Only the DECISION lives here. The money moving back to the client is a
        /// refund <see cref="Payment"/> like any other, because that is what the
        /// client's balance is computed from (see ClientCreditRows).
        /// </para>
        /// </summary>
        public Money? DepositRetainedAmount { get; private set; }

        public DateTime? DepositSettledAt { get; private set; }

        /// <summary>
        /// A deposit was taken and nobody has said what became of it. What the
        /// return screen and the desk's follow-up list ask.
        /// </summary>
        public bool HasUnsettledDeposit =>
            DepositAmount is { Amount: > 0m } && DepositSettledAt is null;
        /// <summary>
        /// What a cancelled hire charges: it REPLACES <see cref="Price"/> on the
        /// client's balance (see ClientCreditRows), so cancelling for free leaves
        /// it null and takes the whole price off.
        /// </summary>
        public Money? CancellationFee { get; private set; }
        public RentingState RentingState { get; private set; } = RentingState.NotYet;
        public string? Notes { get; private set; }

        // Owned outright by this aggregate, so the list is exposed read-only and
        // moves only through AddFee/RemoveFee below.
        private readonly List<RentingFee> _fees = new();

        /// <summary>
        /// What the hire turned out to owe beyond its price — see
        /// <see cref="AddFee"/>.
        /// </summary>
        public IReadOnlyCollection<RentingFee> Fees => _fees;

        public virtual ICollection<ExtraService>? ExtraServices { get; set; }
        public virtual ICollection<RentingHistory>? RentingHistories { get; set; }
        public virtual ICollection<Reservation>? Reservations { get; set; }
        public virtual ICollection<Payment>? Payments { get; set; }
        // Append-only: regenerating issues a new numbered document.
        public virtual ICollection<Contract>? Contracts { get; set; }
        public virtual ICollection<Facture>? Factures { get; set; }
        // The renting doubles as the agency ⇄ client conversation thread.
        public virtual ICollection<ChatMessage>? ChatMessages { get; set; }

        // EF materialisation; stored rows bypass the checks below.
        private Renting() { }

        /// <summary>
        /// Opens an upcoming hire. The price arrives already computed (see
        /// IPricingService); null is a hire at no charge, which is legal.
        /// </summary>
        public static Renting Create(
            int carId,
            int clientId,
            DateTime startDate,
            DateTime endDate,
            Money? price,
            int? startMileage = null,
            int? secondClientId = null,
            Money? depositAmount = null,
            string? notes = null)
        {
            RequirePositive(carId, nameof(carId), "A hire needs a car.");
            RequirePositive(clientId, nameof(clientId), "A hire needs a client.");
            RequirePeriod(startDate, endDate);
            RequireMileage(startMileage, nameof(startMileage));
            RequireNotNegative(price, nameof(price), "The agreed price cannot be negative.");
            RequireNotNegative(depositAmount, nameof(depositAmount), "The deposit cannot be negative.");
            RequireDistinctSecondDriver(clientId, secondClientId);

            return new Renting
            {
                CarId = carId,
                ClientId = clientId,
                SecondClientId = secondClientId,
                StartDate = startDate,
                EndDate = endDate,
                StartMileage = startMileage,
                Price = price,
                DepositAmount = depositAmount,
                Notes = notes,
                RentingState = RentingState.NotYet,
            };
        }

        /// <summary>
        /// The full edit form. The price arrives already decided — kept, re-quoted
        /// or negotiated — because only the caller knows which.
        /// </summary>
        public void Amend(
            int carId,
            int clientId,
            int? secondClientId,
            DateTime startDate,
            DateTime endDate,
            int? startMileage,
            int? endMileage,
            Money? price,
            string? notes)
        {
            RequireLive("edited");
            RequirePositive(carId, nameof(carId), "A hire needs a car.");
            RequirePositive(clientId, nameof(clientId), "A hire needs a client.");
            RequirePeriod(startDate, endDate);
            RequireMileage(startMileage, nameof(startMileage));
            RequireMileage(endMileage, nameof(endMileage));
            RequireReturnAfterPickup(startMileage, endMileage);
            RequireNotNegative(price, nameof(price), "The agreed price cannot be negative.");
            RequireDistinctSecondDriver(clientId, secondClientId);

            CarId = carId;
            ClientId = clientId;
            SecondClientId = secondClientId;
            StartDate = startDate;
            EndDate = endDate;
            StartMileage = startMileage;
            EndMileage = endMileage;
            Price = price;
            Notes = notes;
        }

        /// <summary>
        /// An extension or an early return: the end date moves and the price
        /// follows it, everything else is left alone.
        /// </summary>
        public void ChangeEndDate(DateTime endDate, Money? price)
        {
            RequireLive("changed");

            if (StartDate is DateTime start)
            {
                RequirePeriod(start, endDate);
            }

            RequireNotNegative(price, nameof(price), "The agreed price cannot be negative.");

            EndDate = endDate;
            Price = price;
        }

        /// <summary>The keys change hands: NotYet → InProgress.</summary>
        public void Start(int? pickupMileage = null)
        {
            Require(RentingState.NotYet, "started");
            RequireMileage(pickupMileage, nameof(pickupMileage));

            if (pickupMileage.HasValue)
            {
                StartMileage = pickupMileage;
            }

            RentingState = RentingState.InProgress;
            AddDomainEvent(new RentingStartedEvent(this));
        }

        /// <summary>The car comes back: InProgress → Done.</summary>
        /// <param name="completedAt">Only used to close a hire that has no end date.</param>
        public void Complete(int? returnMileage, DateTime completedAt)
        {
            Require(RentingState.InProgress, "completed");
            RequireMileage(returnMileage, nameof(returnMileage));
            RequireReturnAfterPickup(StartMileage, returnMileage);

            if (returnMileage.HasValue)
            {
                EndMileage = returnMileage;
            }

            EndDate ??= completedAt;
            RentingState = RentingState.Done;
            AddDomainEvent(new RentingCompletedEvent(this));
        }

        /// <summary>
        /// Calls the hire off. The fee is a part of the price kept (see
        /// <see cref="CancellationFee"/>); null cancels for free.
        /// </summary>
        public void Cancel(Money? fee = null)
        {
            RequireLive("cancelled");
            RequireNotNegative(fee, nameof(fee), "The cancellation fee cannot be negative.");

            if (fee is { Amount: > 0m })
            {
                if (Price is not Money price)
                {
                    throw new DomainRuleException(nameof(CancellationFee),
                        "This renting carries no price, so no cancellation fee can be charged on it.");
                }

                if (fee.Currency != price.Currency)
                {
                    throw new DomainRuleException(nameof(CancellationFee),
                        $"The cancellation fee must be in the hire's own currency ({price.Currency}).");
                }

                if (fee.Amount > price.Amount)
                {
                    throw new DomainRuleException(nameof(CancellationFee),
                        $"The cancellation fee cannot exceed the agreed price ({price.Amount}).");
                }
            }

            RentingState = RentingState.Cancelled;
            CancellationFee = fee is { Amount: > 0m } ? fee : null;
            AddDomainEvent(new RentingCancelledEvent(this, CancellationFee));
        }

        /// <summary>
        /// Books an extra charge established at the counter — a late day, a dent,
        /// the kilometres over the allowance. It ADDS to what the hire charges
        /// (see ClientCreditRows) and never touches <see cref="Price"/>, which
        /// stays the agreed price of the rental itself.
        /// <para>
        /// Only on a hire whose car actually went out, and only while the hire is
        /// not cancelled: these are things found on a returning vehicle.
        /// </para>
        /// </summary>
        public RentingFee AddFee(RentingFeeKind kind, Money amount, string? note = null)
        {
            RequireReturnable();
            ArgumentNullException.ThrowIfNull(amount);

            if (amount.Amount <= 0m)
            {
                throw new DomainRuleException(nameof(RentingFee.Amount),
                    "An extra charge must be more than nothing.");
            }

            // The hire's own currency, for the same reason the cancellation fee
            // takes it: these amounts are summed with the price on one invoice.
            // A hire with no price has no currency of its own, so the first
            // charge sets one and the rest follow it — the agency's setting can
            // be changed between two of them, and a total in two currencies is
            // not a total.
            var expected = Price?.Currency ?? _fees.FirstOrDefault()?.Amount?.Currency;

            if (expected is not null && amount.Currency != expected)
            {
                throw new DomainRuleException(nameof(RentingFee.Amount),
                    $"An extra charge must be in the hire's own currency ({expected}).");
            }

            var fee = new RentingFee(kind, amount, note);
            _fees.Add(fee);
            return fee;
        }

        /// <summary>
        /// Takes a charge back off — the counter mistyped it, or the damage
        /// turned out to be already recorded.
        /// <para>
        /// Deliberately unconstrained by state, unlike <see cref="AddFee"/>: a
        /// charge booked on a running hire survives that hire being cancelled
        /// (the charge rule still bills it), so refusing to remove it on a
        /// cancelled hire would strand it on the client's balance with no way
        /// back off.
        /// </para>
        /// </summary>
        public void RemoveFee(RentingFee fee)
        {
            ArgumentNullException.ThrowIfNull(fee);

            if (!_fees.Remove(fee))
            {
                throw new DomainRuleException(nameof(Fees),
                    "That charge does not belong to this hire.");
            }
        }

        /// <summary>
        /// Records what became of the deposit: how much was kept, the rest going
        /// back to the client. Answering it is what closes the question — a
        /// settlement of nothing retained is a decision, not an absence of one.
        /// <para>
        /// Deliberately allowed on any hire that took a deposit, including a
        /// cancelled one: a hire called off after the deposit was collected still
        /// has to say where that money went.
        /// </para>
        /// </summary>
        /// <param name="retained">
        /// How much the agency keeps. Null or zero means the whole deposit goes
        /// back.
        /// </param>
        public void SettleDeposit(Money? retained, DateTime settledAt)
        {
            if (DepositAmount is not Money deposit || deposit.Amount <= 0m)
            {
                throw new DomainRuleException(nameof(DepositRetainedAmount),
                    "This hire took no deposit, so there is nothing to settle.");
            }

            if (retained is { Amount: < 0m })
            {
                throw new DomainRuleException(nameof(DepositRetainedAmount),
                    "A retained amount cannot be negative.");
            }

            if (retained is not null && retained.Currency != deposit.Currency)
            {
                throw new DomainRuleException(nameof(DepositRetainedAmount),
                    $"The retained amount must be in the deposit's own currency ({deposit.Currency}).");
            }

            if (retained is { } kept && kept.Amount > deposit.Amount)
            {
                throw new DomainRuleException(nameof(DepositRetainedAmount),
                    $"The agency cannot keep more than the deposit it holds ({deposit.Amount}).");
            }

            // Zero normalises to null so "nothing retained" and "no amount
            // recorded" are not two states meaning the same thing; SettledAt is
            // what says the question was answered.
            DepositRetainedAmount = retained is { Amount: > 0m } ? retained : null;
            DepositSettledAt = settledAt;
        }

        /// <summary>
        /// Refuses a closed hire early, for callers that would otherwise do
        /// expensive work before reaching the method that refuses it anyway.
        /// </summary>
        public void EnsureLive(string action) => RequireLive(action);

        // Fees describe a car coming back, so a hire that never left (NotYet) and
        // one called off (Cancelled) both refuse them.
        private void RequireReturnable()
        {
            if (RentingState is not (RentingState.InProgress or RentingState.Done))
            {
                throw new InvalidRentingTransitionException(RentingState, "charged extra fees");
            }
        }

        private void RequireLive(string action)
        {
            if (RentingState is RentingState.Done or RentingState.Cancelled)
            {
                throw new InvalidRentingTransitionException(RentingState, action);
            }
        }

        private void Require(RentingState expected, string action)
        {
            if (RentingState != expected)
            {
                throw new InvalidRentingTransitionException(RentingState, action);
            }
        }

        private static void RequirePeriod(DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate)
            {
                throw new DomainRuleException(nameof(EndDate),
                    "The end date must be after the start date.");
            }
        }

        // Either reading being absent says nothing: a hire nobody wrote readings
        // down for is not a hire that drove backwards.
        private static void RequireReturnAfterPickup(int? startMileage, int? endMileage)
        {
            if (startMileage.HasValue && endMileage.HasValue && endMileage < startMileage)
            {
                throw new DomainRuleException(nameof(EndMileage),
                    "The return mileage cannot be less than the pickup mileage.");
            }
        }

        private static void RequireMileage(int? mileage, string property)
        {
            if (mileage < 0)
            {
                throw new DomainRuleException(property, "A mileage reading cannot be negative.");
            }
        }

        private static void RequireNotNegative(Money? amount, string property, string message)
        {
            if (amount is { Amount: < 0m })
            {
                throw new DomainRuleException(property, message);
            }
        }

        private static void RequirePositive(int id, string property, string message)
        {
            if (id <= 0)
            {
                throw new DomainRuleException(property, message);
            }
        }

        private static void RequireDistinctSecondDriver(int clientId, int? secondClientId)
        {
            if (secondClientId == clientId)
            {
                throw new DomainRuleException(nameof(SecondClientId),
                    "The second driver must be a different client from the renter.");
            }
        }
    }
}
