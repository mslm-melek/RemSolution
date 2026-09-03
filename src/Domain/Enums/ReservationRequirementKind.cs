namespace RemSolution.Domain.Enums;

/// <summary>
/// What an agency asks for before it hands the keys over. The kind decides how
/// the customer answers it — money, a file, or a signature — and nothing else:
/// the wording is the requirement's own label, because "a copy of your licence"
/// and "your employer's letter" are the same mechanism.
/// <para>
/// Values are persisted, so they are explicit and never reused.
/// </para>
/// </summary>
public enum ReservationRequirementKind
{
    /// <summary>
    /// Money owed before pickup, settled OFF the platform — a transfer, cash at
    /// the counter, a card machine. The customer answers with a proof (a transfer
    /// slip) and the agency accepts it; the money itself is recorded as a
    /// <c>Payment</c> against the reservation, as it always was.
    /// </summary>
    Payment = 1,

    /// <summary>The refundable deposit, same mechanism as <see cref="Payment"/>.</summary>
    Deposit = 2,

    /// <summary>A document to upload — a licence, a passport, a utility bill.</summary>
    Document = 3,

    /// <summary>Terms the customer has to accept. Answered by accepting, not by a file.</summary>
    Conditions = 4,

    /// <summary>
    /// The rental agreement to sign. Answered by returning a signed copy — the
    /// signature itself is still on paper (see the plan's §2.7).
    /// </summary>
    Contract = 5,
}
