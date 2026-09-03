namespace RemSolution.Domain.Enums;

/// <summary>
/// Where one requirement stands. The happy path is
/// <see cref="Requested"/> → <see cref="Submitted"/> → <see cref="Accepted"/>:
/// the agency asks, the customer answers, the agency looks at the answer.
/// <see cref="Rejected"/> sends it back with a reason and the customer may answer
/// again; <see cref="Waived"/> is the agency deciding it no longer needs it.
/// <para>
/// Only <see cref="Accepted"/> and <see cref="Waived"/> count as settled — see
/// <c>Reservation.HasUnmetRequirements</c>, which is what stops a conversion.
/// </para>
/// </summary>
public enum ReservationRequirementStatus
{
    Requested = 0,
    Submitted = 1,
    Accepted = 2,
    Rejected = 3,
    Waived = 4,
}
