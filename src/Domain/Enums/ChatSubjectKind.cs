namespace RemSolution.Domain.Enums;

/// <summary>
/// Which booking a conversation hangs off. A thread is always about one concrete
/// booking — there is no free-standing conversation — but that booking is either
/// a hire or a hold the agency has confirmed.
/// <para>
/// A hold gets a thread because that is exactly when the customer has questions:
/// what to bring, where to collect, whether the transfer arrived. Waiting for the
/// hire to exist would open the conversation after the moment it was needed.
/// </para>
/// </summary>
public enum ChatSubjectKind
{
    Renting = 1,
    Reservation = 2,
}
