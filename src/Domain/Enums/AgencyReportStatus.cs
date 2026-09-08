namespace RemSolution.Domain.Enums;

/// <summary>
/// Where a report has got to. Only the platform administrator moves it, and only
/// once: a report is arbitrated, not negotiated.
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
}
