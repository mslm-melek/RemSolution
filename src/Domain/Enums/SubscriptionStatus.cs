namespace RemSolution.Domain.Enums;

/// <summary>
/// Managed manually by the platform admin (no billing provider yet).
/// Values are explicit because the "one Active subscription per agency"
/// unique index filters on the stored value of <see cref="Active"/>.
/// </summary>
public enum SubscriptionStatus
{
    Active = 1,
    Suspended = 2,
    Expired = 3,
}
