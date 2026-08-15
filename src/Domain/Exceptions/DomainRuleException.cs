namespace RemSolution.Domain.Exceptions;

/// <summary>
/// An aggregate was handed impossible values (a period that ends before it
/// starts, negative money). Mapped to 400 with the same <c>errors</c> map a
/// FluentValidation failure produces, so callers need no second way to read it.
/// </summary>
public class DomainRuleException : Exception
{
    public DomainRuleException(string property, string message)
        : base(message)
    {
        Property = property;
    }

    // Key of the errors map.
    public string Property { get; }
}
