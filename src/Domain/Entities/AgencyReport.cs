using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A customer's complaint about an agency, arbitrated by the platform
    /// administrator. Raised against a booking the customer actually had — there
    /// is no free-floating report, for the same reason there is no free-floating
    /// review — and settled once, as <see cref="AgencyReportStatus.Upheld"/> or
    /// <see cref="AgencyReportStatus.Dismissed"/>.
    /// <para>
    /// No money moves. An upheld report costs the agency reliability points and
    /// nothing else: until the platform actually holds the money, a debt no
    /// mechanism collects would be a row in a table rather than a sanction.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Deliberately NOT an <c>ITenantEntity</c>, like <see cref="AgencyReview"/>
    /// and for a stronger reason: both people who touch a report are outside the
    /// tenant. The customer who raises it has no agency claim, and the platform
    /// administrator who settles it never carries one, so a global tenant filter
    /// would hide every row from both of them. Agency-facing code must therefore
    /// filter on <see cref="AgencyId"/> by hand — there is no filter to lean on.
    /// <para>
    /// The booking it is about is a tenant row, so what the triage screen needs
    /// to read is snapshotted here (<see cref="BookingSummary"/>,
    /// <see cref="AgencyCancellationReason"/>) rather than joined: a platform
    /// administrator reading reports must not need a cross-tenant bypass to see
    /// which booking one is about.
    /// </para>
    /// </remarks>
    public class AgencyReport : BaseAuditableEntity
    {
        public const int MaxMessageLength = 2000;
        public const int MaxResolutionLength = 2000;

        /// <summary>
        /// How long after a booking ends it can still be complained about. A
        /// window rather than forever: past it the agency can no longer
        /// reasonably answer for what happened, and a score that moves on a
        /// three-year-old booking is one anybody can game.
        /// </summary>
        public const int ReportingWindowDays = 60;

        public int AgencyId { get; private set; }
        public virtual Agency? Agency { get; private set; }

        // The booking complained about: exactly one of the two is set (see
        // Create). Ids only, no navigation — both targets are tenant-filtered and
        // an Include from platform-level code would come back empty.
        public int? ReservationId { get; private set; }
        public int? RentingId { get; private set; }

        /// <summary>The agency's client row for this customer, so it can tell who wrote it.</summary>
        public int? ClientId { get; private set; }
        public virtual Client? Client { get; private set; }

        /// <summary>Identity user id of the marketplace account that raised it.</summary>
        public string? ReporterUserId { get; private set; }

        /// <summary>
        /// Their name at the time, snapshotted like a review's author name: the
        /// report has to stay readable after the client record is renamed.
        /// </summary>
        public string? ReporterName { get; private set; }

        /// <summary>What was booked and when, in one line ("Renault Clio, 12–15 Sep").</summary>
        public string? BookingSummary { get; private set; }

        /// <summary>
        /// What the agency said when it cancelled, copied in at the moment the
        /// report is raised. The customer's account and the agency's are then both
        /// in front of the arbitrator without either side being asked again.
        /// </summary>
        public string? AgencyCancellationReason { get; private set; }

        public AgencyReportKind Kind { get; private set; }

        /// <summary>The customer's account of it, in their own words.</summary>
        public string Message { get; private set; } = string.Empty;

        public AgencyReportStatus Status { get; private set; } = AgencyReportStatus.Open;

        /// <summary>Instant (recorded from the clock), not a wall-clock date.</summary>
        public DateTime SubmittedAt { get; private set; }

        public DateTime? ResolvedAt { get; private set; }
        public string? ResolvedByUserId { get; private set; }

        /// <summary>
        /// The arbitrator's reasoning. Shown to BOTH sides — an outcome nobody
        /// explains is not arbitration — so it is required on either verdict.
        /// </summary>
        public string? ResolutionNote { get; private set; }

        /// <summary>Whether this report is what the score should count against the agency.</summary>
        public bool CountsAgainstAgency => Status == AgencyReportStatus.Upheld;

        /// <summary>
        /// Whether a hold in this state can be complained about: only one the
        /// agency actually confirmed. A request it never answered, refused or let
        /// lapse is a disappointment, not a promise broken.
        /// <para>
        /// A CONVERTED hold is excluded, and that is the "one per booking" rule
        /// rather than a gap: it became a hire, the hire carries its own report,
        /// and the unique indexes are per-anchor — so a hold left reportable after
        /// conversion is one rental that can be complained about twice and cost
        /// the agency two upheld reports.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The "my reservations" projection cannot call this — EF has to
        /// translate the test into SQL — so it repeats the same states inline as
        /// <c>CanReport</c>. Change this rule and that projection changes with it.
        /// <c>AgencyReliabilityCounts.WasConfirmed</c> deliberately does NOT match
        /// this any more: it keeps Converted, because a hire that went ahead is
        /// still a promise the agency kept and belongs in the score's denominator.
        /// </remarks>
        public static bool CanReport(ReservationStatus status, bool cancelledAfterConfirmation) =>
            status is ReservationStatus.Confirmed or ReservationStatus.Paid
            || (status == ReservationStatus.Cancelled && cancelledAfterConfirmation);

        /// <summary>
        /// Whether the window is still open, counted from when the booking ended
        /// (or was called off). A booking with no date to count from never
        /// closes: refusing on a date nobody recorded would be arbitrary.
        /// </summary>
        public static bool IsWithinReportingWindow(DateTime? reference, DateTime now) =>
            reference is not DateTime from || now <= from.AddDays(ReportingWindowDays);

        // EF materialisation; stored rows bypass the checks below.
        private AgencyReport() { }

        public static AgencyReport Create(
            int agencyId,
            AgencyReportKind kind,
            string message,
            DateTime at,
            int? reservationId = null,
            int? rentingId = null,
            int? clientId = null,
            string? reporterUserId = null,
            string? reporterName = null,
            string? bookingSummary = null,
            string? agencyCancellationReason = null)
        {
            // One anchor, always: with neither there is nothing to arbitrate, and
            // with both it is unclear which booking the agency has to answer for.
            if ((reservationId is null) == (rentingId is null))
            {
                throw new DomainRuleException(nameof(ReservationId),
                    "A report is about exactly one booking — a reservation or a renting.");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new DomainRuleException(nameof(Message),
                    "Say what went wrong — a report with no account of it cannot be arbitrated.");
            }

            if (message.Length > MaxMessageLength)
            {
                throw new DomainRuleException(nameof(Message),
                    $"The report cannot be longer than {MaxMessageLength} characters.");
            }

            return new AgencyReport
            {
                AgencyId = agencyId,
                ReservationId = reservationId,
                RentingId = rentingId,
                ClientId = clientId,
                ReporterUserId = reporterUserId,
                ReporterName = reporterName,
                BookingSummary = bookingSummary,
                AgencyCancellationReason = agencyCancellationReason,
                Kind = kind,
                Message = message.Trim(),
                Status = AgencyReportStatus.Open,
                SubmittedAt = at,
            };
        }

        /// <summary>
        /// Drops who raised it, keeping the complaint itself. The report survives
        /// an erasure because it is a record of the AGENCY's conduct — and
        /// <see cref="BookingSummary"/> stays for the same reason: it names a car
        /// and two dates, never a person, and it is all the arbitrator can see of
        /// a booking they cannot read.
        /// </summary>
        public void ErasePersonalData()
        {
            ReporterName = null;
            ReporterUserId = null;
        }

        /// <summary>The platform finds for the customer. Open → Upheld.</summary>
        public void Uphold(string note, DateTime at, string? byUserId) =>
            Resolve(AgencyReportStatus.Upheld, note, at, byUserId);

        /// <summary>The platform finds for the agency. Open → Dismissed.</summary>
        public void Dismiss(string note, DateTime at, string? byUserId) =>
            Resolve(AgencyReportStatus.Dismissed, note, at, byUserId);

        private void Resolve(AgencyReportStatus verdict, string note, DateTime at, string? byUserId)
        {
            // Settled once. Re-opening an arbitration would let a score move
            // twice on one complaint, and neither side could rely on the outcome.
            if (Status != AgencyReportStatus.Open)
            {
                throw new DomainRuleException(nameof(Status),
                    $"This report was already {Status.ToString().ToLowerInvariant()}.");
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                throw new DomainRuleException(nameof(ResolutionNote),
                    "Say why — both sides are shown the outcome.");
            }

            if (note.Length > MaxResolutionLength)
            {
                throw new DomainRuleException(nameof(ResolutionNote),
                    $"The note cannot be longer than {MaxResolutionLength} characters.");
            }

            Status = verdict;
            ResolutionNote = note.Trim();
            ResolvedAt = at;
            ResolvedByUserId = byUserId;
        }
    }
}
