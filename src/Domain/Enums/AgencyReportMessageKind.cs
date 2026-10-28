namespace RemSolution.Domain.Enums;

/// <summary>
/// What a line on a report's thread is. Closing and reopening are written into
/// the thread as well as onto the report, so the history of a report that was
/// withdrawn and reopened is still readable after the second outcome replaces
/// the first.
/// <para>
/// Values are persisted, so they are explicit and never reused.
/// </para>
/// </summary>
public enum AgencyReportMessageKind
{
    /// <summary>An ordinary message from the customer or the platform.</summary>
    Message = 1,

    /// <summary>The customer closed the report; the body is their reason.</summary>
    Withdrawal = 2,

    /// <summary>The customer reopened a report they had withdrawn; the body is their reason.</summary>
    Reopening = 3,
}
