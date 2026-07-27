using System.Globalization;
using System.Resources;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Localization;

namespace RemSolution.Application.UnitTests.Common.Localization;

/// <summary>
/// Pins the load-bearing assumptions of the fr/ar/en setup. Each of these fails
/// silently in production if it breaks — a missing translation just renders the
/// English fallback, and a broken resource path renders the raw key.
/// </summary>
public class LocalizationTests
{
    private static IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>();
    }

    /// <summary>
    /// The resource base name is derived from SharedResource's namespace plus
    /// ResourcesPath. Moving either the type or the .resx folder silently makes
    /// every lookup return its own key.
    /// </summary>
    [Test]
    public void SharedResourceResolvesInsteadOfEchoingTheKey()
    {
        var localizer = CreateLocalizer();

        var value = localizer["Error.Unknown.Title"];

        value.ResourceNotFound.Should().BeFalse(
            "the .resx must live at src/Infrastructure/Resources/SharedResource.resx — see the SharedResource doc comment");
        value.Value.Should().NotBe("Error.Unknown.Title");
    }

    /// <summary>
    /// A key added to the neutral file but forgotten in fr/ar would fall back to
    /// English forever without anyone noticing.
    /// </summary>
    [TestCase(Languages.French)]
    [TestCase(Languages.Arabic)]
    public void EveryNeutralKeyIsTranslated(string language)
    {
        var manager = new ResourceManager(
            "RemSolution.Infrastructure.Resources.SharedResource", typeof(SharedResource).Assembly);

        var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        neutral.Should().NotBeNull();

        var translated = manager.GetResourceSet(new CultureInfo(language), createIfNotExists: true, tryParents: false);
        translated.Should().NotBeNull($"a satellite assembly must be built for '{language}'");

        var missing = neutral!.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .Where(key => translated!.GetString(key) is null)
            .OrderBy(key => key)
            .ToList();

        missing.Should().BeEmpty($"every key must exist in SharedResource.{language}.resx");
    }

    /// <summary>
    /// Validators resolve their message lazily: <c>WithMessage(_ =&gt; localizer[key])</c>.
    /// FluentValidation must still run that resolved string through its message
    /// formatter, otherwise "'{PropertyValue}' is not a known feature." reaches
    /// the user with the placeholder intact.
    /// </summary>
    [Test]
    public void PlaceholdersAreSubstitutedInLazilyResolvedMessages()
    {
        var localizer = CreateLocalizer();
        var validator = new PlaceholderProbeValidator(new ResourceLocalizer(localizer));

        var result = validator.Validate(new PlaceholderProbe { Feature = "NotAFeature" });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Contain("NotAFeature");
        result.Errors[0].ErrorMessage.Should().NotContain("{PropertyValue}");
    }

    /// <summary>
    /// {PropertyName} comes from the C# property name, so it needs the global
    /// display-name resolver to be translated. Configure() is global static
    /// state, so this test restores it.
    /// </summary>
    [Test]
    public void DisplayNameResolverTranslatesPropertyNames()
    {
        var previous = ValidatorOptions.Global.DisplayNameResolver;
        var previousCulture = CultureInfo.CurrentUICulture;

        try
        {
            FluentValidationLocalization.Configure(CreateLocalizer());
            CultureInfo.CurrentUICulture = new CultureInfo(Languages.French);

            var validator = new PlaceholderProbeValidator(new ResourceLocalizer(CreateLocalizer()));
            var result = validator.Validate(new PlaceholderProbe { Feature = "Cars", Currency = null });

            result.IsValid.Should().BeFalse();
            // Property.Currency is "Devise" in French; without the resolver
            // FluentValidation would say "Currency".
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Devise"));
        }
        finally
        {
            ValidatorOptions.Global.DisplayNameResolver = previous;
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    /// <summary>
    /// An unknown property must fall back to FluentValidation's split-PascalCase
    /// default rather than rendering the "Property.X" lookup key.
    /// </summary>
    [Test]
    public void DisplayNameResolverFallsBackForUnmappedProperties()
    {
        var previous = ValidatorOptions.Global.DisplayNameResolver;

        try
        {
            FluentValidationLocalization.Configure(CreateLocalizer());

            var validator = new UnmappedPropertyValidator();
            var result = validator.Validate(new PlaceholderProbe());

            result.IsValid.Should().BeFalse();
            result.Errors[0].ErrorMessage.Should().Contain("Some Unmapped Thing");
            result.Errors[0].ErrorMessage.Should().NotContain("Property.");
        }
        finally
        {
            ValidatorOptions.Global.DisplayNameResolver = previous;
        }
    }

    private class PlaceholderProbe
    {
        public string Feature { get; init; } = string.Empty;
        public string? Currency { get; init; } = "TND";
        public string? SomeUnmappedThing { get; init; }
    }

    private class PlaceholderProbeValidator : AbstractValidator<PlaceholderProbe>
    {
        // Mirrors the shape used by the real validators.
        public PlaceholderProbeValidator(Application.Common.Interfaces.ILocalizer localizer)
        {
            RuleFor(v => v.Feature)
                .Must(FeatureFlags.All.Contains)
                .WithMessage(_ => localizer["Validation.Feature.Unknown"]);

            RuleFor(v => v.Currency).NotEmpty();
        }
    }

    private class UnmappedPropertyValidator : AbstractValidator<PlaceholderProbe>
    {
        public UnmappedPropertyValidator()
        {
            RuleFor(v => v.SomeUnmappedThing).NotEmpty();
        }
    }
}
