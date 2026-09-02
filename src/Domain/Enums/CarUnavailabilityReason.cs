namespace RemSolution.Domain.Enums;

/// <summary>
/// Why a car is off the road for a stretch of dates. Typed so the calendar can
/// label the block itself and so an agency can ask how many days it lost to the
/// garage last quarter; <see cref="Other"/> carries the detail in the note.
/// Values are persisted, so they are explicit and never reused.
/// </summary>
public enum CarUnavailabilityReason
{
    /// <summary>Booked into the garage — servicing, tyres, bodywork.</summary>
    Maintenance = 1,

    /// <summary>Off the road until it is repaired.</summary>
    Repair = 2,

    /// <summary>Technical inspection, insurance, road tax — a paper errand.</summary>
    Administrative = 3,

    /// <summary>Reserved for the agency's own use rather than a client's.</summary>
    InternalUse = 4,

    /// <summary>Anything else, described in the note.</summary>
    Other = 5,
}
