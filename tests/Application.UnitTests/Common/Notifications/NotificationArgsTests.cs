using System.Globalization;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Infrastructure.Notifications;

namespace RemSolution.Application.UnitTests.Common.Notifications;

// A catalog name is stored once, as a translation key or the agency's own words,
// and each reader gets it in their language: one expense alert reaches staff
// reading French, English and Arabic.
public class NotificationArgsTests
{
    private static IReadOnlyDictionary<string, string> Stored(NotificationArgs args) =>
        NotificationArgs.FromJson(args.ToJson());

    [Test]
    public void ACatalogNameMustBeFiledUnderTheSuffixTheRenderersRecognise()
    {
        FluentActions.Invoking(() => new NotificationArgs().SetCatalogName("type", "oilChange"))
            .Should().Throw<ArgumentException>();

        Stored(new NotificationArgs().SetCatalogName("expenseType", "oilChange"))
            .Should().ContainKey("expenseType").WhoseValue.Should().Be("oilChange");
    }

    [Test]
    public void ArabicIsStoredUnescaped()
    {
        // The column is 2000 long; escaped, every Arabic letter costs six.
        new NotificationArgs().Set("car", "تأمين").ToJson().Should().Contain("تأمين");
    }

    [Test]
    public void TheMailRendererTranslatesACatalogKeyForTheRecipient()
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        localizer.Setup(l => l[It.Is<string>(k => k.StartsWith("Notification."))]).Returns("{{expenseType}} — {{car}}");
        localizer.Setup(l => l["CatalogItem.oilChange"])
            .Returns(() => CultureInfo.CurrentUICulture.Name == "ar" ? "تغيير الزيت" : "Vidange");

        var args = Stored(new NotificationArgs()
            .Set("car", "Clio 123 TU 4567")
            .SetCatalogName("expenseType", "oilChange"));

        var renderer = new NotificationTextRenderer(localizer.Object);

        renderer.Render("Any", args, new CultureInfo("ar"), null, null, null)
            .Subject.Should().Be("تغيير الزيت — Clio 123 TU 4567");
        renderer.Render("Any", args, new CultureInfo("fr-TN"), null, null, null)
            .Subject.Should().Be("Vidange — Clio 123 TU 4567");
    }

    [Test]
    public void AnUntranslatedNameReadsAsTyped()
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        localizer.Setup(l => l[It.Is<string>(k => k.StartsWith("Notification."))]).Returns("{{expenseType}}");

        var args = Stored(new NotificationArgs().SetCatalogName("expenseType", "Lavage intérieur"));

        new NotificationTextRenderer(localizer.Object)
            .Render("Any", args, new CultureInfo("ar"), null, null, null)
            .Subject.Should().Be("Lavage intérieur");
    }
}
