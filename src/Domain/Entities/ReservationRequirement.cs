using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One thing the agency asks for before it hands over the keys: a transfer,
    /// the deposit, a copy of a licence, terms to accept, the agreement to sign.
    /// <para>
    /// The customer answers it from their own screen and the agency looks at the
    /// answer — so the state below is the record of a conversation, not a
    /// checkbox. That is why a rejection carries its reason: the customer has to
    /// be told what to send instead, and three weeks later somebody has to be
    /// able to see what was asked and when it was answered.
    /// </para>
    /// <para>
    /// Money is NOT settled here. A requirement is the ASK; the money that
    /// arrives is a <c>Payment</c> against the reservation, as it always was —
    /// keeping the client's balance computed from the ledger and nowhere else.
    /// </para>
    /// </summary>
    public class ReservationRequirement : BaseAuditableEntity, ITenantEntity
    {
        public const int MaxLabelLength = 200;
        public const int MaxNoteLength = 1000;

        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int ReservationId { get; private set; }
        public virtual Reservation? Reservation { get; private set; }

        public ReservationRequirementKind Kind { get; private set; }

        /// <summary>What is being asked for, in the agency's own words.</summary>
        public string Label { get; private set; } = string.Empty;

        /// <summary>
        /// How much, for a payment or a deposit. Null for everything else, and
        /// null on a payment too when the agency only wants to say "settle the
        /// balance" without naming a figure.
        /// </summary>
        public Money? Amount { get; private set; }

        /// <summary>
        /// How the agency expects to be paid. Nothing enforces it — the money
        /// arrives off the platform — but it is what the customer is told to do.
        /// </summary>
        public PaymentMethod? ExpectedMethod { get; private set; }

        public ReservationRequirementStatus Status { get; private set; }
            = ReservationRequirementStatus.Requested;

        /// <summary>The customer's answer: a transfer slip, a scan, a signed contract.</summary>
        public int? SubmittedFileId { get; private set; }
        public virtual StoredFile? SubmittedFile { get; private set; }

        /// <summary>What the customer said with it — a transfer reference, usually.</summary>
        public string? SubmittedNote { get; private set; }

        /// <summary>Instant (recorded from the clock), not a wall-clock date.</summary>
        public DateTime? SubmittedAt { get; private set; }

        public DateTime? ReviewedAt { get; private set; }
        public string? ReviewedBy { get; private set; }

        /// <summary>Why it was refused, or why it was dropped. Shown to the customer.</summary>
        public string? ReviewNote { get; private set; }

        /// <summary>Settled: the agency has what it asked for, or no longer wants it.</summary>
        public bool IsSettled =>
            Status is ReservationRequirementStatus.Accepted or ReservationRequirementStatus.Waived;

        // EF materialisation; stored rows bypass the checks below.
        private ReservationRequirement() { }

        public static ReservationRequirement Create(
            int reservationId,
            ReservationRequirementKind kind,
            string label,
            Money? amount = null,
            PaymentMethod? expectedMethod = null)
        {
            if (reservationId <= 0)
            {
                throw new DomainRuleException(nameof(ReservationId),
                    "A requirement belongs to a reservation.");
            }

            RequireLabel(label);

            if (amount is not null && amount.Amount < 0)
            {
                throw new DomainRuleException(nameof(Amount),
                    "A requirement cannot ask for a negative amount.");
            }

            return new ReservationRequirement
            {
                ReservationId = reservationId,
                Kind = kind,
                Label = label.Trim(),
                Amount = amount,
                ExpectedMethod = expectedMethod,
                Status = ReservationRequirementStatus.Requested,
            };
        }

        /// <summary>
        /// The customer answers. Allowed from <c>Requested</c> and from
        /// <c>Rejected</c> — a refusal is a request to try again, not an end.
        /// </summary>
        public void Submit(int? fileId, string? note, DateTime at)
        {
            Require(
                ReservationRequirementStatus.Requested,
                ReservationRequirementStatus.Rejected);

            RequireNote(note);

            // Accepting terms carries no file; everything else is answered with
            // one, and a "submission" with neither says nothing at all.
            if (fileId is null && Kind != ReservationRequirementKind.Conditions
                && string.IsNullOrWhiteSpace(note))
            {
                throw new DomainRuleException(nameof(SubmittedFileId),
                    "Answer this with a file or a note.");
            }

            SubmittedFileId = fileId;
            SubmittedNote = Trimmed(note);
            SubmittedAt = at;
            Status = ReservationRequirementStatus.Submitted;

            // A second attempt starts a clean review.
            ReviewedAt = null;
            ReviewedBy = null;
            ReviewNote = null;
        }

        /// <summary>
        /// The agency is satisfied. Allowed straight from <c>Requested</c> too:
        /// most of these are settled at the counter, and the agent ticking off a
        /// deposit paid in cash should not have to fake a submission first.
        /// </summary>
        public void Accept(DateTime at, string? by, string? note = null)
        {
            Require(
                ReservationRequirementStatus.Requested,
                ReservationRequirementStatus.Submitted,
                ReservationRequirementStatus.Rejected);

            RequireNote(note);

            Status = ReservationRequirementStatus.Accepted;
            ReviewedAt = at;
            ReviewedBy = by;
            ReviewNote = Trimmed(note);
        }

        /// <summary>
        /// Sent back, with the reason the customer is shown. Allowed from the
        /// unanswered state for the same reason <see cref="Accept"/> is: what the
        /// customer handed over at the counter can be refused without ever having
        /// passed through the screen.
        /// </summary>
        public void Reject(DateTime at, string? by, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainRuleException(nameof(ReviewNote),
                    "Say why it was refused — the customer has to know what to send instead.");
            }

            Require(
                ReservationRequirementStatus.Requested,
                ReservationRequirementStatus.Submitted,
                ReservationRequirementStatus.Accepted);

            RequireNote(reason);

            Status = ReservationRequirementStatus.Rejected;
            ReviewedAt = at;
            ReviewedBy = by;
            ReviewNote = reason.Trim();
        }

        /// <summary>The agency drops the requirement. Allowed from any state.</summary>
        public void Waive(DateTime at, string? by, string? reason = null)
        {
            RequireNote(reason);

            Status = ReservationRequirementStatus.Waived;
            ReviewedAt = at;
            ReviewedBy = by;
            ReviewNote = Trimmed(reason);
        }

        /// <summary>Re-labels or re-prices an ask that has not been answered yet.</summary>
        public void Amend(string label, Money? amount, PaymentMethod? expectedMethod)
        {
            Require(ReservationRequirementStatus.Requested);
            RequireLabel(label);

            Label = label.Trim();
            Amount = amount;
            ExpectedMethod = expectedMethod;
        }

        private void Require(params ReservationRequirementStatus[] allowed)
        {
            if (!allowed.Contains(Status))
            {
                throw new DomainRuleException(nameof(Status),
                    $"A requirement that is {Status} cannot move on from there.");
            }
        }

        private static void RequireLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new DomainRuleException(nameof(Label),
                    "Say what is being asked for.");
            }

            if (label.Length > MaxLabelLength)
            {
                throw new DomainRuleException(nameof(Label),
                    $"The label cannot be longer than {MaxLabelLength} characters.");
            }
        }

        private static void RequireNote(string? note)
        {
            if (note?.Length > MaxNoteLength)
            {
                throw new DomainRuleException(nameof(ReviewNote),
                    $"The note cannot be longer than {MaxNoteLength} characters.");
            }
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
