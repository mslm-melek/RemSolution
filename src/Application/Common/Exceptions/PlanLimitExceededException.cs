namespace RemSolution.Application.Common.Exceptions;

/// <summary>
/// Thrown when creating a resource would exceed the agency's subscription plan
/// quota. Mapped to 409 Conflict.
/// </summary>
public class PlanLimitExceededException : Exception
{
    public PlanLimitExceededException(string resource, int limit)
        : base($"The subscription plan allows at most {limit} {resource}. Upgrade the plan to add more.")
    {
        Resource = resource;
        Limit = limit;
    }

    public string Resource { get; }
    public int Limit { get; }
}
