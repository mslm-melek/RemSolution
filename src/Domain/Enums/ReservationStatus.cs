namespace RemSolution.Domain.Enums;

/// <summary>
/// Lifecycle of a reservation — a hold on a specific car for a date range.
/// The happy path is
/// <see cref="PendingConfirmation"/> → <see cref="Confirmed"/> →
/// (<see cref="Paid"/>) → <see cref="Converted"/>: a customer/agent places a
/// hold, the agency confirms it, payment may be recorded, and finally it is
/// converted into an actual renting. Off-ramps are <see cref="Rejected"/>
/// (agency declines — the client is told why), <see cref="Expired"/> (a pending
/// hold lapsed past its <c>ExpiresAt</c>, swept by a background job), and
/// <see cref="Cancelled"/> (cancelled within the agency's cancellation window).
///
/// Only <see cref="Confirmed"/> and <see cref="Paid"/> block a car's
/// availability. <see cref="PendingConfirmation"/> deliberately does NOT: a
/// request is a question, not a claim, so several customers may ask for the same
/// car and the same dates and the agency decides between them. The conflict is
/// enforced at confirmation instead — see the confirm command, which refuses
/// while another booking holds the period. <see cref="Converted"/> (the renting
/// now blocks instead), <see cref="Rejected"/>, <see cref="Expired"/> and
/// <see cref="Cancelled"/> do not block either.
///
/// Underlying values 0–3 are unchanged from the original hold model so persisted
/// rows keep their meaning (the old <c>Pending</c> is now
/// <see cref="PendingConfirmation"/>); 4–6 are the states added in Phase 3.
/// </summary>
public enum ReservationStatus
{
    PendingConfirmation = 0,
    Confirmed = 1,
    Cancelled = 2,
    Expired = 3,
    Rejected = 4,
    Paid = 5,
    Converted = 6,
}
