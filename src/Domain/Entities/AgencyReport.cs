using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A customer's complaint about an agency, arbitrated by the platform
    /// administrator. Raised against a booking the customer actually had — there
    /// is no free-floating report, for the same reason there is no free-floating
    /// review — and settled once, as <see cref="AgencyReportStatus.Upheld"/> or
    /// <see cref="AgencyReportStatus.Dismissed"/>, unless the customer withdraws
    /// it first. While it is open the customer and the platform can write to each
    /// other on it (<see cref="Messages"/>); once closed, it is frozen — except
    /// that the customer may reopen a withdrawal once (<see cref="Reopen"/>).
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
    public class AgencyReport : BaseAuditableEntity, IHasRowVersion
    {
        public const int MaxMessageLength = 2000;
        public const int MaxResolutionLength = 2000;
        public const int MaxEmailLength = 256;

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

        /// <summary>
        /// Where the customer asked to be written to when the platform replies or
        /// decides. Optional, given on the report rather than read off the account,
        /// because it is the customer's choice where this lands. Cleared on erasure.
        /// </summary>
        public string? NotifyEmail { get; private set; }

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
        /// Why it was closed, required whichever way: the arbitrator's reasoning on
        /// a verdict (shown to BOTH sides — an outcome nobody explains is not
        /// arbitration), or the customer's own reason on a withdrawal.
        /// <see cref="Status"/> says which.
        /// </summary>
        public string? ResolutionNote { get; private set; }

        /// <summary>
        /// When the customer reopened it after withdrawing it. A report reopens
        /// once: a complaint that can be dropped and revived at will is a lever
        /// on the agency, not a request for arbitration.
        /// </summary>
        public DateTime? ReopenedAt { get; private set; }

        /// <summary>
        /// How many lines the thread has. Stored rather than counted because
        /// incrementing it is what makes a message an UPDATE of this row, and so
        /// subject to <see cref="RowVersion"/>: a message racing a verdict loses
        /// with a concurrency error instead of landing on a closed report. A
        /// counter, not a timestamp — two writes in the same clock tick would
        /// leave a timestamp unchanged, and EF would then skip the UPDATE.
        /// </summary>
        public int MessageCount { get; private set; }

        /// <summary>
        /// Two people can now close a report — the platform deciding and the
        /// customer withdrawing — so two closings starting from the same open row
        /// must not both succeed, the second silently overwriting the first.
        /// </summary>
        public byte[]? RowVersion { get; set; }

        // Owned outright by this aggregate, so the list is exposed read-only and
        // grows only through Post, Withdraw and Reopen below.
        private readonly List<AgencyReportMessage> _messages = new();

        /// <summary>
        /// What the customer and the platform said to each other after the report
        /// was raised, plus a line for each withdrawal and reopening.
        /// <see cref="Message"/> stays the opening account.
        /// </summary>
        public IReadOnlyCollection<AgencyReportMessage> Messages => _messages;

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
            string? agencyCancellationReason = null,
            string? notifyEmail = null)
        {
            // One anchor, always: with neither there is nothing to arbitrate, and
            // with both it is unclear which booking the agency has to answer for.
            if ((reservationId is null) == (rentingId is null))
            {
                throw new DomainRuleException(nameof(ReservationId), "AgencyReport.OneBooking",
                    "A report is about exactly one booking — a reservation or a renting.");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new DomainRuleException(nameof(Message), "AgencyReport.MessageRequired",
                    "Say what went wrong — a report with no account of it cannot be arbitrated.");
            }

            if (message.Length > MaxMessageLength)
            {
                throw new DomainRuleException(nameof(Message), "AgencyReport.MessageTooLong",
                    $"The report cannot be longer than {MaxMessageLength} characters.", MaxMessageLength);
            }

            if (notifyEmail?.Trim().Length > MaxEmailLength)
            {
                throw new DomainRuleException(nameof(NotifyEmail), "AgencyReport.EmailTooLong",
                    $"The email address cannot be longer than {MaxEmailLength} characters.", MaxEmailLength);
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
                NotifyEmail = string.IsNullOrWhiteSpace(notifyEmail) ? null : notifyEmail.Trim(),
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
        /// <remarks>
        /// The thread must be loaded. The customer's follow-ups are emptied (see
        /// <see cref="AgencyReportMessage.Erase"/>), and so is a withdrawal's
        /// reason; <see cref="Message"/> and a verdict's
        /// <see cref="ResolutionNote"/> stay, being the complaint and its outcome.
        /// </remarks>
        public void ErasePersonalData()
        {
            var reporter = ReporterUserId;

            ReporterName = null;
            ReporterUserId = null;
            NotifyEmail = null;

            // The same id sits in two more columns: the customer created the row,
            // and closed it if they withdrew it. (UpdatedBy needs nothing here —
            // the save that writes this erasure restamps it with whoever erases.)
            if (reporter is not null)
            {
                if (CreatedBy == reporter) CreatedBy = null;
                if (ResolvedByUserId == reporter) ResolvedByUserId = null;
            }

            // A withdrawal's outcome is the customer's own free text, unlike a
            // verdict's, which the platform wrote.
            if (Status == AgencyReportStatus.Withdrawn)
            {
                ResolutionNote = null;
            }

            foreach (var message in _messages.Where(m => !m.FromPlatform))
            {
                message.Erase();
            }
        }

        /// <summary>The customer adds to their account while the report is open.</summary>
        public AgencyReportMessage PostFromCustomer(string body, DateTime at, string? byUserId)
        {
            // A settled report is frozen: a message after the verdict would read
            // as an appeal, and there is none.
            RequireOpen();
            return Append(AgencyReportMessageKind.Message, fromPlatform: false, body, at, byUserId);
        }

        /// <summary>The platform asks or tells the customer something while the report is open.</summary>
        public AgencyReportMessage PostFromPlatform(string body, DateTime at, string? byUserId)
        {
            RequireOpen();
            return Append(AgencyReportMessageKind.Message, fromPlatform: true, body, at, byUserId);
        }

        /// <summary>The platform finds for the customer. Open → Upheld.</summary>
        public void Uphold(string note, DateTime at, string? byUserId) =>
            Close(AgencyReportStatus.Upheld, note, at, byUserId);

        /// <summary>The platform finds for the agency. Open → Dismissed.</summary>
        public void Dismiss(string note, DateTime at, string? byUserId) =>
            Close(AgencyReportStatus.Dismissed, note, at, byUserId);

        /// <summary>
        /// The customer closes their own report before the platform decides.
        /// Open → Withdrawn. The reason goes on the report as its outcome and into
        /// the thread, where it survives a later reopening.
        /// </summary>
        public void Withdraw(string reason, DateTime at, string? byUserId)
        {
            // Both checks before either change: a reason that fits the outcome
            // but not the thread must not leave a half-withdrawn report tracked.
            RequireOpen();
            RequireBody(reason);

            Close(AgencyReportStatus.Withdrawn, reason, at, byUserId);
            Append(AgencyReportMessageKind.Withdrawal, fromPlatform: false, reason, at, byUserId);
        }

        /// <summary>
        /// Whether a report in this state can still be reopened by the customer:
        /// withdrawn, never reopened before, and within
        /// <see cref="ReportingWindowDays"/> of the withdrawal — the case it exists
        /// for is a promise made to get the report dropped and then not kept, which
        /// shows within weeks, not years.
        /// </summary>
        /// <remarks>
        /// "My reports" repeats this inline as <c>CanReopen</c>, because EF has to
        /// translate it. Change one and change the other.
        /// </remarks>
        public static bool CanReopen(
            AgencyReportStatus status, DateTime? reopenedAt, DateTime? withdrawnAt, DateTime now) =>
            status == AgencyReportStatus.Withdrawn
            && reopenedAt is null
            && withdrawnAt is DateTime closed
            && now <= closed.AddDays(ReportingWindowDays);

        /// <summary>
        /// The customer takes back their withdrawal. Withdrawn → Open, once. The
        /// withdrawal stays readable in the thread; the report itself goes back to
        /// having no outcome, so the platform can decide it as if it had never
        /// been dropped.
        /// </summary>
        public void Reopen(string reason, DateTime at, string? byUserId)
        {
            if (!CanReopen(Status, ReopenedAt, ResolvedAt, at))
            {
                throw Status != AgencyReportStatus.Withdrawn
                    ? new DomainRuleException(nameof(Status), "AgencyReport.ReopenNotWithdrawn",
                        "Only a report you withdrew can be reopened.")
                    : ReopenedAt is not null
                        ? new DomainRuleException(nameof(Status), "AgencyReport.AlreadyReopened",
                            "This report was already reopened once.")
                        : new DomainRuleException(nameof(Status), "AgencyReport.ReopenWindowClosed",
                            $"A report can be reopened within {ReportingWindowDays} days of withdrawing it.",
                            ReportingWindowDays);
            }

            RequireBody(reason);

            Status = AgencyReportStatus.Open;
            ResolutionNote = null;
            ResolvedAt = null;
            ResolvedByUserId = null;
            ReopenedAt = at;

            Append(AgencyReportMessageKind.Reopening, fromPlatform: false, reason, at, byUserId);
        }

        private AgencyReportMessage Append(
            AgencyReportMessageKind kind, bool fromPlatform, string body, DateTime at, string? byUserId)
        {
            RequireBody(body);

            var message = new AgencyReportMessage(kind, fromPlatform, body.Trim(), at, byUserId);
            _messages.Add(message);

            // Also what turns a message into an update of this row (see MessageCount).
            MessageCount++;

            return message;
        }

        private static void RequireBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new DomainRuleException(nameof(Messages), "AgencyReport.EmptyMessage",
                    "The message is empty.");
            }

            if (body.Length > MaxMessageLength)
            {
                throw new DomainRuleException(nameof(Messages), "AgencyReport.ThreadMessageTooLong",
                    $"A message cannot be longer than {MaxMessageLength} characters.", MaxMessageLength);
            }
        }

        private void RequireOpen()
        {
            // Settled once. Re-opening an arbitration would let a score move
            // twice on one complaint, and neither side could rely on the outcome.
            if (Status != AgencyReportStatus.Open)
            {
                // One key per outcome: the status word is not translatable on its own.
                throw new DomainRuleException(nameof(Status), $"AgencyReport.Already{Status}",
                    $"This report was already {Status.ToString().ToLowerInvariant()}.");
            }
        }

        private void Close(AgencyReportStatus outcome, string note, DateTime at, string? byUserId)
        {
            RequireOpen();
            RequireNote(note);

            Status = outcome;
            ResolutionNote = note.Trim();
            ResolvedAt = at;
            ResolvedByUserId = byUserId;
        }

        private static void RequireNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new DomainRuleException(nameof(ResolutionNote), "AgencyReport.NoteRequired",
                    "Say why — the outcome is shown to the other side.");
            }

            if (note.Length > MaxResolutionLength)
            {
                throw new DomainRuleException(nameof(ResolutionNote), "AgencyReport.NoteTooLong",
                    $"The note cannot be longer than {MaxResolutionLength} characters.", MaxResolutionLength);
            }
        }
    }
}
