namespace RemSolution.Domain.ValueObjects;

/// <summary>
/// How dependable an agency has been, from the two things it can be held to:
/// bookings it confirmed and then called off itself, and reports a platform
/// administrator upheld against it. The arithmetic lives here alone so the
/// marketplace shopfront and the agency's own screen cannot quote different
/// numbers for the same agency.
/// <para>
/// The counts travel with the score for the same reason they do on the client
/// side: "88" means nothing next to "two of sixty confirmed bookings cancelled".
/// </para>
/// <para>
/// Unlike the client score, this one is PUBLIC — an agency is a shop window, and
/// what it does to customers is what a customer is entitled to see before
/// booking. A customer's record stays with the agency they built it at.
/// </para>
/// </summary>
public sealed record AgencyReliability(
    int ConfirmedBookings,
    int CancelledByAgency,
    int UpheldReports)
{
    // What breaking a confirmed booking costs, and what being found at fault on
    // top of it costs. The same scale as the client score, deliberately: the two
    // are read side by side and a point has to mean the same thing on both.
    public const int CancellationPenalty = 15;
    public const int UpheldReportPenalty = 15;

    /// <summary>Confirmed bookings the agency saw through.</summary>
    public int Honoured => Math.Max(ConfirmedBookings - CancelledByAgency, 0);

    /// <summary>
    /// The score, or null when the agency has nothing on record yet. Null rather
    /// than 100: a brand-new agency has not earned a perfect record, and the
    /// shopfront says "no bookings yet" instead — the same choice the review
    /// average makes for an unrated agency.
    /// </summary>
    public int? Score => ConfirmedBookings == 0 && UpheldReports == 0
        ? null
        : Math.Clamp(
            100
            - (CancelledByAgency * CancellationPenalty)
            - (UpheldReports * UpheldReportPenalty),
            0,
            100);
}
