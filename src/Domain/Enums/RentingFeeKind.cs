namespace RemSolution.Domain.Enums;

/// <summary>
/// What an extra charge recorded when the car comes back is FOR. Typed rather
/// than free text so the invoice can label the line itself, and so an agency can
/// ask what late returns earned it last quarter — <see cref="Other"/> is there
/// for the case the list does not cover, with the note carrying the detail.
/// </summary>
public enum RentingFeeKind
{
    /// <summary>Kept the car past the agreed hour.</summary>
    Late = 0,

    /// <summary>Damage found on the vehicle at handover.</summary>
    Damage = 1,

    /// <summary>Kilometres driven over the allowance.</summary>
    ExcessMileage = 2,

    /// <summary>Returned short of fuel.</summary>
    Fuel = 3,

    /// <summary>Returned needing more than the usual valet.</summary>
    Cleaning = 4,

    /// <summary>Anything else, described in the note.</summary>
    Other = 5,
}
