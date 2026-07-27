using System.Globalization;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Infrastructure.Documents;

/// <summary>
/// Issues contracts and invoices for a renting; see
/// <see cref="IRentalDocumentService"/> for the transaction / SaveChanges /
/// authorization contract this relies on.
/// <para>
/// The shape of the document comes from a template (the agency's, or the
/// platform's shipped example); this class is the plumbing between the booking's
/// data and that template.
/// </para>
/// </summary>
public class RentalDocumentService : IRentalDocumentService
{
    private const string ContractPrefix = "CTR";
    private const string FacturePrefix = "FAC";

    private readonly IApplicationDbContext _context;
    private readonly IStoredFileService _storedFiles;
    private readonly IRentalDocumentRenderer _renderer;
    private readonly IAgencySettingsProvider _settings;
    private readonly DocumentTemplateExamples _examples;
    private readonly ILocalizer _localizer;
    private readonly TimeProvider _dateTime;

    public RentalDocumentService(
        IApplicationDbContext context,
        IStoredFileService storedFiles,
        IRentalDocumentRenderer renderer,
        IAgencySettingsProvider settings,
        DocumentTemplateExamples examples,
        ILocalizer localizer,
        TimeProvider dateTime)
    {
        _context = context;
        _storedFiles = storedFiles;
        _renderer = renderer;
        _settings = settings;
        _examples = examples;
        _localizer = localizer;
        _dateTime = dateTime;
    }

    public async Task<Contract> GenerateContractAsync(
        RentalDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var renting = await LoadRentingAsync(request.RentingId, cancellationToken);
        var context = await BuildContextAsync(renting, cancellationToken);

        var template = await ResolveTemplateAsync(
            DocumentTemplateKind.Contract, request.TemplateId, context.Language, cancellationToken);

        var year = context.IssuedAt.Year;
        var sequence = await NextContractSequenceAsync(year, cancellationToken);
        var number = FormatNumber(ContractPrefix, year, sequence);

        var source = BaseSource(renting, context, number);
        var rendered = Resolve(template, source, request.ManualValues, lineItems: null, totals: null);

        var file = await StoreAsync(
            _renderer.Render(rendered), renting.AgencyId, "contracts", number,
            DocumentType.RentalContract, cancellationToken);

        var contract = new Contract
        {
            RentingId = renting.Id,
            Year = year,
            SequenceNumber = sequence,
            Number = number,
            IssuedAt = context.IssuedAt,
            DocumentFile = file,
            Language = context.Language,
            DocumentTemplateId = template.Id,
            TemplateName = template.Name
            // AgencyId is stamped by TenantEntityInterceptor on insert.
        };

        _context.Contracts.Add(contract);
        return contract;
    }

    public async Task<Facture> GenerateFactureAsync(
        RentalDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var renting = await LoadRentingAsync(request.RentingId, cancellationToken);
        var context = await BuildContextAsync(renting, cancellationToken);

        var template = await ResolveTemplateAsync(
            DocumentTemplateKind.Facture, request.TemplateId, context.Language, cancellationToken);

        var year = context.IssuedAt.Year;
        var sequence = await NextFactureSequenceAsync(year, cancellationToken);
        var number = FormatNumber(FacturePrefix, year, sequence);

        var billing = await BuildBillingAsync(renting, context.Currency, cancellationToken);

        var source = BaseSource(renting, context, number) with
        {
            RentalAmount = billing.Rental,
            ExtraServicesAmount = billing.ExtrasTotal,
            Total = billing.Total,
            AmountPaid = billing.Paid,
            BalanceDue = billing.Total - billing.Paid
        };

        var rendered = Resolve(
            template, source, request.ManualValues,
            billing.Lines(context, _localizer), billing.Totals(context, _localizer));

        var file = await StoreAsync(
            _renderer.Render(rendered), renting.AgencyId, "factures", number,
            DocumentType.RentalFacture, cancellationToken);

        var facture = new Facture
        {
            RentingId = renting.Id,
            ClientId = renting.ClientId,
            Year = year,
            SequenceNumber = sequence,
            Number = number,
            IssuedAt = context.IssuedAt,
            RentalAmount = Money.Of(billing.Rental, context.Currency),
            ExtraServicesAmount = Money.Of(billing.ExtrasTotal, context.Currency),
            TotalAmount = Money.Of(billing.Total, context.Currency),
            DocumentFile = file,
            Language = context.Language,
            DocumentTemplateId = template.Id,
            TemplateName = template.Name
        };

        _context.Factures.Add(facture);
        return facture;
    }

    public async Task<IReadOnlyList<DocumentTemplateField>> GetPromptFieldsAsync(
        DocumentTemplateKind kind, int? templateId, CancellationToken cancellationToken = default)
    {
        var language = CurrentLanguage();
        var template = await ResolveTemplateAsync(kind, templateId, language, cancellationToken);

        // Only the placeholders actually USED by the blocks are worth prompting
        // for: an editor may leave a stale binding behind after deleting a block.
        var used = DocumentTemplateBlocks.FindPlaceholders(template.Blocks).ToHashSet(StringComparer.Ordinal);

        return template.Fields
            .Where(f => f.Binding == DocumentFieldBinding.AskEachTime && used.Contains(f.Placeholder))
            .ToList();
    }

    // Substitutes the template against the data and packages it for the renderer.
    private RenderedDocument Resolve(
        ResolvedTemplate template,
        DocumentDataSource source,
        IReadOnlyDictionary<string, string>? manualValues,
        IReadOnlyList<RenderedLineItem>? lineItems,
        IReadOnlyList<RenderedLineItem>? totals)
    {
        var dataValues = DocumentPlaceholderResolver.Resolve(source);

        var resolution = DocumentTemplateResolver.Resolve(
            template.Blocks, template.Fields, dataValues, manualValues);

        if (resolution.MissingRequired.Count > 0)
        {
            // Reported as a validation failure per placeholder so the SPA can
            // highlight the inputs it already prompted for.
            throw new ValidationException(resolution.MissingRequired
                .Select(placeholder => new ValidationFailure(
                    placeholder, _localizer["Validation.Document.RequiredFieldMissing", placeholder]))
                .ToList());
        }

        return new RenderedDocument
        {
            Language = source.Language,
            Blocks = resolution.Blocks,
            LineItems = lineItems ?? Array.Empty<RenderedLineItem>(),
            Totals = totals ?? Array.Empty<RenderedLineItem>()
        };
    }

    /// <summary>
    /// The template to use: the one asked for, else the agency's default for this
    /// kind and language, else the platform's shipped example. Nothing here can
    /// return null — an agency that has never opened the template screen still
    /// gets paperwork.
    /// </summary>
    private async Task<ResolvedTemplate> ResolveTemplateAsync(
        DocumentTemplateKind kind, int? templateId, string language, CancellationToken cancellationToken)
    {
        // Tenant-scoped by the global query filter, so an id from another agency
        // simply is not found.
        var query = _context.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.Fields)
            .Where(t => t.Kind == kind);

        // Retired templates are unusable either way: "retire" is what the admin was
        // told it does, and a retired default already falls through to the example,
        // so honouring an explicit id would be the one way around it.
        var template = templateId is int id
            ? await query.FirstOrDefaultAsync(t => t.IsActive && t.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(
                t => t.IsActive && t.IsDefault && t.Language == language, cancellationToken);

        if (templateId is not null && template is null)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(RentalDocumentRequest.TemplateId),
                    _localizer["Validation.Document.TemplateNotFound"])
            });
        }

        if (template is not null)
        {
            return new ResolvedTemplate(
                template.Id,
                template.Name,
                DocumentTemplateBlocks.Deserialize(template.BlocksJson),
                template.Fields?.ToList() ?? new List<DocumentTemplateField>());
        }

        var example = _examples.Default(kind, language);

        // Id null records "the shipped example produced this" (see Contract).
        return new ResolvedTemplate(null, example.Name, example.Blocks, new List<DocumentTemplateField>());
    }

    private async Task<Renting> LoadRentingAsync(int rentingId, CancellationToken cancellationToken)
    {
        // Tracked (not AsNoTracking): the caller may have just added this renting
        // in the same unit of work, and a tracked query still returns it.
        var renting = await _context.Rentings
            .Include(r => r.Car).ThenInclude(c => c!.Model)
            .Include(r => r.Client)
            .Include(r => r.SecondClient)
            .FirstOrDefaultAsync(r => r.Id == rentingId, cancellationToken);

        Guard.Against.NotFound(rentingId, renting);

        return renting;
    }

    // Everything a document needs that comes from the agency rather than the
    // booking: who the lessor is, the currency, the issue timestamp and the
    // language to render in.
    private async Task<DocumentContext> BuildContextAsync(Renting renting, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(renting.AgencyId, cancellationToken);

        // Agency is not an ITenantEntity, so this is an explicit id lookup rather
        // than a filtered read; renting.AgencyId is the tenant's own by construction.
        var agency = await _context.Agencies
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == renting.AgencyId, cancellationToken);

        return new DocumentContext(
            new RentalDocumentAgency(
                agency?.Name ?? string.Empty, agency?.Address, agency?.PhoneNumber, agency?.Email),
            settings.CurrencyCode,
            _dateTime.GetUtcNow().UtcDateTime,
            CurrentLanguage());
    }

    // The language of the request that asked for the document; a background caller
    // with no request culture gets the product default.
    private static string CurrentLanguage() =>
        Languages.Normalize(CultureInfo.CurrentUICulture.Name) ?? Languages.Default;

    private static DocumentDataSource BaseSource(Renting renting, DocumentContext context, string number) => new()
    {
        Language = context.Language,
        Currency = context.Currency,
        Number = number,
        IssuedAt = context.IssuedAt,
        Agency = context.Agency,
        Client = Party(renting.Client),
        SecondDriver = renting.SecondClient is null ? null : Party(renting.SecondClient),
        Car = Car(renting.Car),
        StartDate = renting.StartDate,
        EndDate = renting.EndDate,
        StartMileage = renting.StartMileage,
        Price = renting.Price?.Amount,
        DepositAmount = renting.DepositAmount?.Amount,
        Notes = renting.Notes
    };

    private async Task<Billing> BuildBillingAsync(
        Renting renting, string currency, CancellationToken cancellationToken)
    {
        var rental = renting.Price?.Amount ?? 0m;

        // One line per extra service, labelled by its type. Nullable projections
        // keep a service with no amount out of the arithmetic instead of throwing
        // on a missing owned Money.
        var extras = await _context.ExtraServices
            .Where(e => e.RentingId == renting.Id)
            .OrderBy(e => e.Id)
            .Select(e => new ExtraLine(
                e.ExtraServicesType != null ? e.ExtraServicesType.Name : null,
                (decimal?)e.TotalAmount!.Amount))
            .ToListAsync(cancellationToken);

        // Net of everything recorded against the renting: refunds are stored as
        // negative amounts and a reversal is an offsetting entry, so the plain
        // sum is what has actually been collected.
        var paid = await _context.Payments
            .Where(p => p.RentingId == renting.Id)
            .Select(p => (decimal?)p.PayementAmount!.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        return new Billing(rental, extras, paid, currency);
    }

    // The printed form of a sequence: "CTR-2026-000042". Zero-padded so numbers
    // sort lexically the way they sort numerically, which is what a folder of
    // exported PDFs relies on.
    private static string FormatNumber(string prefix, int year, int sequence) =>
        $"{prefix}-{year}-{sequence:D6}";

    private async Task<int> NextContractSequenceAsync(int year, CancellationToken cancellationToken)
    {
        // Tenant-scoped by the global query filter, and safe only under the
        // per-agency write lock the caller holds (see IRentalDocumentService).
        var last = await _context.Contracts
            .Where(c => c.Year == year)
            .Select(c => (int?)c.SequenceNumber)
            .MaxAsync(cancellationToken);

        return (last ?? 0) + 1;
    }

    private async Task<int> NextFactureSequenceAsync(int year, CancellationToken cancellationToken)
    {
        var last = await _context.Factures
            .Where(f => f.Year == year)
            .Select(f => (int?)f.SequenceNumber)
            .MaxAsync(cancellationToken);

        return (last ?? 0) + 1;
    }

    private async Task<StoredFile> StoreAsync(
        byte[] pdf,
        int agencyId,
        string folder,
        string number,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        var fileName = $"{number}.pdf";

        // The number is unique per agency, so it is also a safe unique path — no
        // Guid suffix needed, and the stored path stays greppable.
        var relativePath = $"agencies/{agencyId}/{folder}/{fileName}";

        using var content = new MemoryStream(pdf, writable: false);

        return await _storedFiles.CreateAsync(
            content, fileName, "application/pdf", documentType, relativePath, cancellationToken);
    }

    private static RentalDocumentParty Party(Client? client) =>
        new(
            $"{client?.FirstName} {client?.LastName}".Trim(),
            client?.FirstName,
            client?.LastName,
            client?.BirthDate,
            client?.BirthPlace,
            client?.CIN,
            client?.CINDeliveranceDate,
            client?.CINDeliverancePlace,
            client?.PasseportNumber,
            client?.DrivingLicenceNumber,
            client?.DrivingLicenceDeliveranceDate,
            client?.Description);

    private static RentalDocumentCar Car(Domain.Entities.Car? car) =>
        new(
            car?.Model?.Name,
            car?.Matricule,
            car?.Color,
            car?.Power,
            car?.FuelType?.ToString());

    private sealed record DocumentContext(
        RentalDocumentAgency Agency,
        string Currency,
        DateTime IssuedAt,
        string Language);

    private sealed record ExtraLine(string? Label, decimal? Amount);

    // The invoice's money, in one place so the rows the renderer draws and the
    // totals snapshotted on the Facture row cannot drift apart.
    private sealed record Billing(decimal Rental, List<ExtraLine> Extras, decimal Paid, string Currency)
    {
        public decimal ExtrasTotal => Extras.Sum(e => e.Amount ?? 0m);

        public decimal Total => Rental + ExtrasTotal;

        public IReadOnlyList<RenderedLineItem> Lines(DocumentContext context, ILocalizer localizer)
        {
            string Amount(decimal? value) =>
                DocumentPlaceholderResolver.FormatAmount(value, Currency, context.Language);

            // The rental charge is always the first row; extra services follow in
            // the order they were added.
            var lines = new List<RenderedLineItem>
            {
                new(localizer["Document.RentalLine"], Amount(Rental))
            };

            lines.AddRange(Extras.Select(e => new RenderedLineItem(
                string.IsNullOrWhiteSpace(e.Label) ? localizer["Document.Description"] : e.Label,
                Amount(e.Amount ?? 0m))));

            return lines;
        }

        public IReadOnlyList<RenderedLineItem> Totals(DocumentContext context, ILocalizer localizer)
        {
            string Amount(decimal? value) =>
                DocumentPlaceholderResolver.FormatAmount(value, Currency, context.Language);

            return new List<RenderedLineItem>
            {
                new(localizer["Document.Total"], Amount(Total)),
                new(localizer["Document.AmountPaid"], Amount(Paid)),
                new(localizer["Document.BalanceDue"], Amount(Total - Paid)),
            };
        }
    }

    // A template flattened to what generation needs, whether it came from a row or
    // from a shipped example (which has no id).
    private sealed record ResolvedTemplate(
        int? Id,
        string Name,
        IReadOnlyList<DocumentBlock> Blocks,
        IReadOnlyList<DocumentTemplateField> Fields);
}
