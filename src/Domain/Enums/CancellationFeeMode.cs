namespace RemSolution.Domain.Enums;

/// <summary>
/// How an agency charges for a booking called off late. Off by default: an
/// agency opts into charging its customers, it is not opted in by an upgrade.
/// </summary>
public enum CancellationFeeMode
{
    /// <summary>Cancelling costs nothing, whenever it happens.</summary>
    None = 0,

    /// <summary>A flat amount in the agency's currency.</summary>
    FixedAmount = 1,

    /// <summary>
    /// A share of what the booking was priced at. A booking with no price
    /// carries no fee — there is nothing to take a share of.
    /// </summary>
    PercentOfPrice = 2,
}
