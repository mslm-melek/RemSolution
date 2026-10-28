namespace RemSolution.Domain.Enums;

/// <summary>
/// Where a report has got to. The platform arbitrates it once — a report is
/// arbitrated, not negotiated — unless the customer who raised it withdraws it
/// first, which they may take back once.
/// <para>
/// Values are persisted, so they are explicit and never reused.
/// </para>
/// </summary>
public enum AgencyReportStatus
{
    /// <summary>Raised by the customer, waiting for the platform to look at it.</summary>
    Open = 1,

    /// <summary>
    /// The platform agrees with the customer. Costs the agency reliability
    /// points on top of whatever the underlying fact already cost.
    /// </summary>
    Upheld = 2,

    /// <summary>
    /// The platform does not. Costs nothing — and the resolution note says why,
    /// because the customer is told the outcome either way.
    /// </summary>
    Dismissed = 3,

    /// <summary>
    /// Closed by the customer before the platform decided, with a reason (the
    /// matter was settled with the agency, they changed their mind…). Costs the
    /// agency nothing, exactly like a dismissal, since nobody found it at fault.
    /// The one outcome that is not final: the customer can reopen it once.
    /// </summary>
    Withdrawn = 4,
}
