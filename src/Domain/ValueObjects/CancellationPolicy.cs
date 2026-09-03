using RemSolution.Domain.Enums;

namespace RemSolution.Domain.ValueObjects;

/// <summary>
/// What a customer owes for calling a booking off, and when. The agency sets
/// three figures and this turns them into an answer, so the SPA that warns
/// "cancelling now costs 45 TND" and the command that records it cannot disagree.
/// <para>
/// Three bands, and the two boundaries are separate settings on purpose:
/// <list type="bullet">
/// <item>earlier than <c>freeHours</c> before the start — free;</item>
/// <item>between that and <c>cutoffHours</c> — allowed, and it costs the fee;</item>
/// <item>later than <c>cutoffHours</c> — refused outright, which is the rule that
/// already existed (see the cancel commands).</item>
/// </list>
/// A booking with no start date has no bands at all: nothing is late when nothing
/// is scheduled, so it is always free.
/// </para>
/// </summary>
public sealed record CancellationPolicy(
    CancellationFeeMode Mode,
    decimal Value,
    int FreeHours,
    int CutoffHours)
{
    /// <summary>Cancelling costs nothing at all under this policy.</summary>
    public bool ChargesNothing => Mode == CancellationFeeMode.None || Value <= 0m;

    /// <summary>
    /// Whether a cancellation at <paramref name="now"/> falls in the band that
    /// carries the fee. False before the free window closes, and false once the
    /// booking can no longer be cancelled at all.
    /// </summary>
    public bool IsLate(DateTime? startDate, DateTime now)
    {
        if (startDate is not DateTime start) return false;

        return now >= start.AddHours(-FreeHours);
    }

    /// <summary>
    /// The fee for calling off a booking priced at <paramref name="price"/>, or
    /// null when nothing is owed. Rounded, like every stored amount.
    /// </summary>
    public Money? FeeFor(Money? price, DateTime? startDate, DateTime now, string currency)
    {
        if (ChargesNothing || !IsLate(startDate, now)) return null;

        var amount = Mode switch
        {
            CancellationFeeMode.FixedAmount => Value,
            // A share of nothing is nothing: an unpriced booking carries no fee
            // rather than a fee the customer cannot check.
            CancellationFeeMode.PercentOfPrice => (price?.Amount ?? 0m) * Value / 100m,
            _ => 0m,
        };

        if (amount <= 0m) return null;

        // Never more than the booking itself: calling a hire off cannot cost more
        // than taking it (the same ceiling the renting cancel path applies).
        if (price is not null && amount > price.Amount)
        {
            amount = price.Amount;
        }

        return Money.Of(amount, price?.Currency ?? currency).Round();
    }
}
