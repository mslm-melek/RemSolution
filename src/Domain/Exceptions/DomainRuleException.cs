namespace RemSolution.Domain.Exceptions;

/// <summary>
/// An aggregate was handed impossible values (a period that ends before it
/// starts, negative money). Mapped to 400 with the same <c>errors</c> map a
/// FluentValidation failure produces, so callers need no second way to read it.
/// <para>
/// The Domain has no localizer, so a rule names its wording by key and the API
/// translates it (<c>DomainRule.{key}</c> in the shared resx, positional
/// <c>{0}</c> arguments). <see cref="Exception.Message"/> stays the English text,
/// for the logs and as the answer when a key has no translation.
/// </para>
/// </summary>
public class DomainRuleException : Exception
{
    public DomainRuleException(string property, string messageKey, string message, params object[] arguments)
        : base(message)
    {
        Property = property;
        MessageKey = messageKey;
        Arguments = arguments;
    }

    // Key of the errors map.
    public string Property { get; }

    public string MessageKey { get; }

    public object[] Arguments { get; }
}
