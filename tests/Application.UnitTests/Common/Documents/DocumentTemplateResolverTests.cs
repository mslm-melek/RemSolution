using FluentAssertions;
using NUnit.Framework;
using RemSolution.Application.Common.Documents;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.UnitTests.Common.Documents;

/// <summary>
/// The binding rules: what a placeholder resolves to, and what happens when it
/// cannot resolve. These decide what ends up printed on a legal document, so each
/// branch is pinned rather than left to the integration tests.
/// </summary>
public class DocumentTemplateResolverTests
{
    private static readonly IReadOnlyDictionary<string, string> Data =
        new Dictionary<string, string>
        {
            [DocumentPlaceholders.ClientFullName] = "Amina Ben Salah",
            [DocumentPlaceholders.CarMatricule] = "123 TU 4567",
            [DocumentPlaceholders.SecondDriverFullName] = string.Empty
        };

    /// <summary>
    /// The case that makes a template usable with no setup: a placeholder named
    /// after a data path resolves to it even with no binding row.
    /// </summary>
    [Test]
    public void ResolvesAKnownPathWithNoBinding()
    {
        var result = Resolve("Renter: {{client.fullName}}", Array.Empty<DocumentTemplateField>());

        Text(result).Should().Be("Renter: Amina Ben Salah");
        result.MissingRequired.Should().BeEmpty();
    }

    [Test]
    public void ResolvesADataFieldBindingThroughItsPath()
    {
        // A placeholder called anything, pointed at a known path.
        var field = new DocumentTemplateField
        {
            Placeholder = "plaque",
            Binding = DocumentFieldBinding.DataField,
            DataPath = DocumentPlaceholders.CarMatricule
        };

        Text(Resolve("Vehicle {{plaque}}", new[] { field })).Should().Be("Vehicle 123 TU 4567");
    }

    [Test]
    public void ResolvesAFixedValue()
    {
        var field = new DocumentTemplateField
        {
            Placeholder = "franchise",
            Binding = DocumentFieldBinding.FixedValue,
            FixedValue = "500 TND"
        };

        Text(Resolve("Franchise: {{franchise}}", new[] { field })).Should().Be("Franchise: 500 TND");
    }

    [Test]
    public void ResolvesAnAskEachTimeValueFromWhatTheAgentSupplied()
    {
        var field = AskEachTime("franchise", required: true);

        var result = Resolve("Franchise: {{franchise}}", new[] { field },
            manual: new Dictionary<string, string> { ["franchise"] = "300 TND" });

        Text(result).Should().Be("Franchise: 300 TND");
        result.MissingRequired.Should().BeEmpty();
    }

    /// <summary>
    /// A required ask-each-time field with nothing supplied must be REPORTED, not
    /// quietly blanked — generation refuses rather than printing a contract with a
    /// hole in it.
    /// </summary>
    [Test]
    public void ReportsARequiredAskEachTimeValueThatIsMissing()
    {
        var result = Resolve("Franchise: {{franchise}}", new[] { AskEachTime("franchise", required: true) });

        result.MissingRequired.Should().Equal("franchise");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void TreatsABlankSuppliedValueAsMissing(string supplied)
    {
        var result = Resolve("Franchise: {{franchise}}", new[] { AskEachTime("franchise", required: true) },
            manual: new Dictionary<string, string> { ["franchise"] = supplied });

        result.MissingRequired.Should().Equal("franchise");
    }

    [Test]
    public void DoesNotReportAnOptionalAskEachTimeValue()
    {
        var result = Resolve("Note: {{note}}", new[] { AskEachTime("note", required: false) });

        result.MissingRequired.Should().BeEmpty();
        Text(result).Should().Be("Note: ");
    }

    /// <summary>Reported once, however many times the placeholder appears.</summary>
    [Test]
    public void ReportsAMissingRequiredValueOnlyOnce()
    {
        var result = Resolve("{{franchise}} and again {{franchise}}",
            new[] { AskEachTime("franchise", required: true) });

        result.MissingRequired.Should().Equal("franchise");
    }

    [Test]
    public void RendersABlankBindingAsARuleToWriteOn()
    {
        var field = new DocumentTemplateField
        {
            Placeholder = "signedAt",
            Binding = DocumentFieldBinding.Blank
        };

        Text(Resolve("Signed at {{signedAt}}", new[] { field }))
            .Should().Be("Signed at ______________");
    }

    /// <summary>
    /// An unbound name that is not a data path either. Printing the raw braces on a
    /// document handed to a customer would be worse than printing nothing.
    /// </summary>
    [Test]
    public void SubstitutesAnUnknownPlaceholderWithNothing()
    {
        Text(Resolve("Value: {{nobodyKnows}}", Array.Empty<DocumentTemplateField>()))
            .Should().Be("Value: ");
    }

    /// <summary>
    /// Placeholders are substituted in every templated string of a block, not just
    /// its body text — a field label or a signature line can carry one too.
    /// </summary>
    [Test]
    public void SubstitutesInsideFieldsAndSignatureLabels()
    {
        var blocks = new List<DocumentBlock>
        {
            new()
            {
                Type = DocumentBlockType.Fields,
                Title = "For {{client.fullName}}",
                Fields = new List<DocumentBlockField>
                {
                    new() { Label = "Plate", Value = "{{car.matricule}}" }
                }
            },
            new()
            {
                Type = DocumentBlockType.Signatures,
                Labels = new List<string> { "Signed by {{client.fullName}}" }
            }
        };

        var result = DocumentTemplateResolver.Resolve(blocks, Array.Empty<DocumentTemplateField>(), Data);

        result.Blocks[0].Title.Should().Be("For Amina Ben Salah");
        result.Blocks[0].Fields![0].Value.Should().Be("123 TU 4567");
        result.Blocks[1].Labels![0].Should().Be("Signed by Amina Ben Salah");
    }

    /// <summary>
    /// A path that exists but holds nothing (no second driver) resolves to empty, so
    /// the template's own hide-when-empty rule can take effect.
    /// </summary>
    [Test]
    public void ResolvesAnAbsentValueToEmpty()
    {
        Text(Resolve("Driver: {{secondDriver.fullName}}", Array.Empty<DocumentTemplateField>()))
            .Should().Be("Driver: ");
    }

    // --- auto-binding, the "recognised → mapped, rest → ask" rule ---

    [Test]
    public void AutoBindsAKnownPathToItself()
    {
        var field = DocumentTemplateFields.AutoBind(
            DocumentPlaceholders.ClientCin, DocumentTemplateKind.Contract);

        field.Binding.Should().Be(DocumentFieldBinding.DataField);
        field.DataPath.Should().Be(DocumentPlaceholders.ClientCin);
    }

    [Test]
    public void AutoBindsAnUnknownNameToAskEachTime()
    {
        var field = DocumentTemplateFields.AutoBind("franchise", DocumentTemplateKind.Contract);

        field.Binding.Should().Be(DocumentFieldBinding.AskEachTime);
        field.Label.Should().Be("franchise");
    }

    /// <summary>
    /// Invoice totals are not available on a contract, so a contract template using
    /// one gets an ask-each-time binding rather than a path that would always print
    /// blank.
    /// </summary>
    [Test]
    public void DoesNotAutoBindAnInvoiceOnlyPathOnAContract()
    {
        DocumentTemplateFields.AutoBind(DocumentPlaceholders.FactureTotal, DocumentTemplateKind.Contract)
            .Binding.Should().Be(DocumentFieldBinding.AskEachTime);

        DocumentTemplateFields.AutoBind(DocumentPlaceholders.FactureTotal, DocumentTemplateKind.Facture)
            .Binding.Should().Be(DocumentFieldBinding.DataField);
    }

    [Test]
    public void ReconcileAddsBindingsForPlaceholdersNobodyMentioned()
    {
        var blocks = Blocks("{{client.fullName}} pays {{franchise}}");

        var fields = DocumentTemplateFields.Reconcile(
            blocks, Array.Empty<DocumentTemplateField>(), DocumentTemplateKind.Contract);

        fields.Should().HaveCount(2);
        fields.Single(f => f.Placeholder == "client.fullName").Binding
            .Should().Be(DocumentFieldBinding.DataField);
        fields.Single(f => f.Placeholder == "franchise").Binding
            .Should().Be(DocumentFieldBinding.AskEachTime);
    }

    [Test]
    public void ReconcileKeepsAnExplicitBindingRatherThanAutoBindingOverIt()
    {
        var supplied = new DocumentTemplateField
        {
            Placeholder = DocumentPlaceholders.ClientFullName,
            Binding = DocumentFieldBinding.FixedValue,
            FixedValue = "ANONYMOUS"
        };

        var fields = DocumentTemplateFields.Reconcile(
            Blocks("{{client.fullName}}"), new[] { supplied }, DocumentTemplateKind.Contract);

        fields.Should().ContainSingle()
            .Which.Binding.Should().Be(DocumentFieldBinding.FixedValue);
    }

    /// <summary>
    /// A binding for a placeholder no longer in the blocks is KEPT: an admin mid-edit
    /// who temporarily deleted a block should not silently lose its configuration.
    /// </summary>
    [Test]
    public void ReconcileKeepsBindingsForPlaceholdersNoLongerUsed()
    {
        var stale = AskEachTime("removed", required: true);

        var fields = DocumentTemplateFields.Reconcile(
            Blocks("{{client.fullName}}"), new[] { stale }, DocumentTemplateKind.Contract);

        fields.Should().HaveCount(2);
        fields.Should().Contain(f => f.Placeholder == "removed");
    }

    private static DocumentTemplateField AskEachTime(string placeholder, bool required) => new()
    {
        Placeholder = placeholder,
        Binding = DocumentFieldBinding.AskEachTime,
        Label = placeholder,
        IsRequired = required
    };

    private static List<DocumentBlock> Blocks(string text) => new()
    {
        new DocumentBlock { Type = DocumentBlockType.Paragraph, Text = text }
    };

    private static DocumentTemplateResolution Resolve(
        string text,
        IEnumerable<DocumentTemplateField> fields,
        IReadOnlyDictionary<string, string>? manual = null) =>
        DocumentTemplateResolver.Resolve(Blocks(text), fields, Data, manual);

    private static string? Text(DocumentTemplateResolution resolution) => resolution.Blocks[0].Text;
}
