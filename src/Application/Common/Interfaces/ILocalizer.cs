namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Resolves a user-facing message for the current request's culture.
/// <para>
/// Thin abstraction over <c>IStringLocalizer</c> so the Application layer can
/// localize validation and domain messages without taking a dependency on the
/// ASP.NET localization stack. Missing keys resolve to the key itself, which
/// keeps a typo visible rather than blank.
/// </para>
/// </summary>
public interface ILocalizer
{
    string this[string key] { get; }

    string this[string key, params object[] arguments] { get; }
}
