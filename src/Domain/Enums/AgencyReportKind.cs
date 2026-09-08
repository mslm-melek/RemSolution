namespace RemSolution.Domain.Enums;

/// <summary>
/// What a customer is complaining about. The kind only routes and labels the
/// report — it changes nothing about how it is arbitrated, because the platform
/// administrator reads the same thing either way: what happened, and whether the
/// agency was in the wrong.
/// <para>
/// Values are persisted, so they are explicit and never reused.
/// </para>
/// </summary>
public enum AgencyReportKind
{
    /// <summary>
    /// The agency called off a booking it had already confirmed. The only kind
    /// the score penalises on its own, before anybody arbitrates: the fact is
    /// not in dispute, only whether the agency had cause.
    /// </summary>
    CancelledBooking = 1,

    /// <summary>The agency did not deliver what it promised, or behaved badly.</summary>
    ServiceQuality = 2,

    /// <summary>A charge the customer says was not owed, or a deposit not returned.</summary>
    Billing = 3,

    /// <summary>The car itself: not the one booked, not roadworthy, not clean.</summary>
    Vehicle = 4,

    /// <summary>Anything else. Read by a person, so it does not need a taxonomy.</summary>
    Other = 5,
}
