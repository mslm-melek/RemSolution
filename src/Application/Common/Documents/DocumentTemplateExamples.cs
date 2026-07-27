using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Documents;

/// <summary>
/// The platform's read-only starting points: a standard rental agreement and a
/// standard invoice, in whichever language is asked for.
/// <para>
/// These are CODE, not rows in <c>DocumentTemplates</c>. Three reasons: the
/// tenancy model stays clean (no nullable-AgencyId rows every query would have to
/// remember to include), improving an example immediately reaches every agency
/// that has not cloned it, and the labels can come from the shared .resx so one
/// definition serves fr/ar/en. Cloning materialises an example into a real,
/// editable, tenant-owned template.
/// </para>
/// <para>
/// They also double as the fallback: an agency that has never opened the template
/// screen still gets sensible paperwork, which is why these reproduce the layout
/// the product shipped before templates existed.
/// </para>
/// </summary>
public class DocumentTemplateExamples
{
    /// <summary>
    /// A shipped example: an identity stable enough to clone by, plus its blocks.
    /// <paramref name="Key"/> is what the API and SPA pass around — it is not a
    /// database id, because there is no row.
    /// </summary>
    public sealed record Example(
        string Key,
        string Name,
        DocumentTemplateKind Kind,
        string Language,
        IReadOnlyList<DocumentBlock> Blocks);

    public const string StandardContractKey = "standard-contract";
    public const string StandardFactureKey = "standard-facture";

    private readonly ILocalizer _localizer;

    public DocumentTemplateExamples(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Every example available for a language, for the "clone one" picker.</summary>
    public IReadOnlyList<Example> All(string language) => new[]
    {
        Get(StandardContractKey, language)!,
        Get(StandardFactureKey, language)!,
    };

    /// <summary>The example with this key, or null if the key is unknown.</summary>
    public Example? Get(string key, string language) => key switch
    {
        StandardContractKey => new Example(
            StandardContractKey,
            _localizer["Document.Example.StandardContract"],
            DocumentTemplateKind.Contract,
            language,
            StandardContract()),
        StandardFactureKey => new Example(
            StandardFactureKey,
            _localizer["Document.Example.StandardFacture"],
            DocumentTemplateKind.Facture,
            language,
            StandardFacture()),
        _ => null
    };

    /// <summary>
    /// The example used when an agency has no template of its own for this kind.
    /// </summary>
    public Example Default(DocumentTemplateKind kind, string language) =>
        Get(kind == DocumentTemplateKind.Facture ? StandardFactureKey : StandardContractKey, language)!;

    private List<DocumentBlock> StandardContract() => new()
    {
        Heading(_localizer["Document.Contract.Title"]),
        Reference(),

        // The lessor / renter pair, drawn as two boxes on one row.
        Lessor(),
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            SideBySide = true,
            Title = _localizer["Document.Renter"],
            Fields = new List<DocumentBlockField>
            {
                Field(string.Empty, DocumentPlaceholders.ClientFullName),
                Field(_localizer["Document.BirthDate"], DocumentPlaceholders.ClientBirthDate, hideWhenEmpty: true),
                Field(_localizer["Document.CIN"], DocumentPlaceholders.ClientCin, hideWhenEmpty: true),
                Field(_localizer["Document.Passeport"], DocumentPlaceholders.ClientPasseportNumber, hideWhenEmpty: true),
                Field(_localizer["Document.DrivingLicence"], DocumentPlaceholders.ClientDrivingLicenceNumber, hideWhenEmpty: true),
            }
        },

        // Every row hides when empty, so the whole box disappears for a booking
        // with no additional driver (see the renderer).
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = _localizer["Document.SecondDriver"],
            Fields = new List<DocumentBlockField>
            {
                Field(string.Empty, DocumentPlaceholders.SecondDriverFullName, hideWhenEmpty: true),
                Field(_localizer["Document.CIN"], DocumentPlaceholders.SecondDriverCin, hideWhenEmpty: true),
                Field(_localizer["Document.DrivingLicence"], DocumentPlaceholders.SecondDriverDrivingLicenceNumber, hideWhenEmpty: true),
            }
        },

        Vehicle(),

        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = _localizer["Document.RentalPeriod"],
            Fields = new List<DocumentBlockField>
            {
                Field(_localizer["Document.From"], DocumentPlaceholders.RentingStartDate),
                Field(_localizer["Document.To"], DocumentPlaceholders.RentingEndDate),
                Field(_localizer["Document.PickupMileage"], DocumentPlaceholders.RentingStartMileage, hideWhenEmpty: true),
            }
        },

        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = _localizer["Document.Price"],
            Fields = new List<DocumentBlockField>
            {
                Field(_localizer["Document.Price"], DocumentPlaceholders.RentingPrice),
                Field(_localizer["Document.Deposit"], DocumentPlaceholders.RentingDeposit, hideWhenEmpty: true),
            }
        },

        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = _localizer["Document.Notes"],
            Fields = new List<DocumentBlockField>
            {
                Field(string.Empty, DocumentPlaceholders.RentingNotes, hideWhenEmpty: true),
            }
        },

        new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Fine = true,
            Text = _localizer["Document.ContractTerms"]
        },

        new DocumentBlock
        {
            Type = DocumentBlockType.Signatures,
            Labels = new List<string>
            {
                _localizer["Document.SignatureLessor"],
                _localizer["Document.SignatureRenter"],
            }
        },
    };

    private List<DocumentBlock> StandardFacture() => new()
    {
        Heading(_localizer["Document.Facture.Title"]),
        Reference(),

        Lessor(),
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            SideBySide = true,
            Title = _localizer["Document.Renter"],
            Fields = new List<DocumentBlockField>
            {
                Field(string.Empty, DocumentPlaceholders.ClientFullName),
                Field(_localizer["Document.CIN"], DocumentPlaceholders.ClientCin, hideWhenEmpty: true),
                Field(_localizer["Document.DrivingLicence"], DocumentPlaceholders.ClientDrivingLicenceNumber, hideWhenEmpty: true),
            }
        },

        Vehicle(),

        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = _localizer["Document.RentalPeriod"],
            Fields = new List<DocumentBlockField>
            {
                Field(_localizer["Document.From"], DocumentPlaceholders.RentingStartDate),
                Field(_localizer["Document.To"], DocumentPlaceholders.RentingEndDate),
            }
        },

        // Rows and totals come from the booking, not the template.
        new DocumentBlock
        {
            Type = DocumentBlockType.LineItems,
            ShowTotals = true
        },
    };

    private DocumentBlock Reference() => new()
    {
        Type = DocumentBlockType.Paragraph,
        Fine = true,
        Text = $"{_localizer["Document.Number"]} {Token(DocumentPlaceholders.DocumentNumber)}"
             + $" — {_localizer["Document.IssuedAt"]} {Token(DocumentPlaceholders.DocumentIssuedAt)}"
    };

    private DocumentBlock Lessor() => new()
    {
        Type = DocumentBlockType.Fields,
        SideBySide = true,
        Title = _localizer["Document.Lessor"],
        Fields = new List<DocumentBlockField>
        {
            Field(string.Empty, DocumentPlaceholders.AgencyName),
            Field(string.Empty, DocumentPlaceholders.AgencyAddress, hideWhenEmpty: true),
            Field(string.Empty, DocumentPlaceholders.AgencyPhone, hideWhenEmpty: true),
            Field(string.Empty, DocumentPlaceholders.AgencyEmail, hideWhenEmpty: true),
        }
    };

    private DocumentBlock Vehicle() => new()
    {
        Type = DocumentBlockType.Fields,
        Title = _localizer["Document.Vehicle"],
        Fields = new List<DocumentBlockField>
        {
            Field(_localizer["Document.Model"], DocumentPlaceholders.CarModel, hideWhenEmpty: true),
            Field(_localizer["Document.Matricule"], DocumentPlaceholders.CarMatricule, hideWhenEmpty: true),
            Field(_localizer["Document.Color"], DocumentPlaceholders.CarColor, hideWhenEmpty: true),
            Field(_localizer["Document.Power"], DocumentPlaceholders.CarPower, hideWhenEmpty: true),
            Field(_localizer["Document.FuelType"], DocumentPlaceholders.CarFuelType, hideWhenEmpty: true),
        }
    };

    private static DocumentBlock Heading(string text) =>
        new() { Type = DocumentBlockType.Heading, Text = text };

    private static DocumentBlockField Field(string label, string placeholderPath, bool hideWhenEmpty = false) =>
        new() { Label = label, Value = Token(placeholderPath), HideWhenEmpty = hideWhenEmpty };

    /// <summary>Wraps a data path as the placeholder token an admin would type.</summary>
    private static string Token(string placeholderPath) => $"{{{{{placeholderPath}}}}}";
}
