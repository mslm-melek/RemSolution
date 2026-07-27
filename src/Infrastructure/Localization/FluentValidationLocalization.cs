using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace RemSolution.Infrastructure.Localization;

/// <summary>
/// Localizes the parts of FluentValidation that are not covered by explicit
/// <c>WithMessage</c> calls.
/// <para>
/// FluentValidation already ships translations for its built-in rule messages
/// ("'{PropertyName}' must not be empty.") and picks them from
/// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>, which the
/// request-localization middleware sets. What it cannot translate is
/// <c>{PropertyName}</c> — that is derived from the C# property name. This hooks
/// the global display-name resolver so a property looks up
/// <c>Property.&lt;Name&gt;</c> in the shared resources first, and only falls back
/// to FluentValidation's split-PascalCase default when there is no entry.
/// </para>
/// </summary>
public static class FluentValidationLocalization
{
    private static readonly Regex SplitPascalCase =
        new("((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", RegexOptions.Compiled);

    public static void Configure(IStringLocalizer<SharedResource> localizer)
    {
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
        {
            if (member is null) return null;

            var localized = localizer[$"Property.{member.Name}"];

            // ResourceManagerStringLocalizer reports ResourceNotFound and echoes
            // the key back; that is the signal to use the default naming.
            return localized.ResourceNotFound
                ? SplitPascalCase.Replace(member.Name, " $1").Trim()
                : localized.Value;
        };
    }
}
