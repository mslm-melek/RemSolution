namespace RemSolution.Application.Common.Exceptions;

/// <summary>
/// Thrown when a tenant tries to write without an Active, in-period
/// subscription. Mapped to 402 Payment Required. Reads are never blocked.
/// </summary>
public class SubscriptionRequiredException : Exception
{
    public SubscriptionRequiredException()
        : base("The agency has no active subscription. Writes are blocked until the subscription is renewed.")
    {
    }
}
