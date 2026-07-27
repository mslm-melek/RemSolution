using Microsoft.Extensions.Localization;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Infrastructure.Localization;

/// <summary>
/// <see cref="ILocalizer"/> over the shared .resx set. Registered as a singleton
/// because <see cref="IStringLocalizer{T}"/> is itself culture-agnostic: it reads
/// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> at lookup
/// time, which the request-localization middleware sets per request.
/// </summary>
public class ResourceLocalizer : ILocalizer
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResourceLocalizer(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public string this[string key] => _localizer[key];

    public string this[string key, params object[] arguments] => _localizer[key, arguments];
}
