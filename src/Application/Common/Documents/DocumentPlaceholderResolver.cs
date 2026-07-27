using System.Globalization;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Common.Documents;

/// <summary>
/// Turns a <see cref="DocumentDataSource"/> into the value for every path in
/// <see cref="DocumentPlaceholders"/>. This is the one place a path name meets the
/// data behind it — the catalog says what exists, this says what it holds.
/// <para>
/// A path with nothing behind it (no second driver, no deposit) resolves to an
/// EMPTY STRING, never to a dash or the path name: the template decides how an
/// absent value looks (drop the row, or leave a blank to complete by hand), and it
/// cannot decide that if the resolver has already printed something.
/// </para>
/// <para>
/// Formatting uses the document's own culture, so a document rendered by a
/// background job reads the same as one rendered in a request.
/// </para>
/// </summary>
public static class DocumentPlaceholderResolver
{
    public static IReadOnlyDictionary<string, string> Resolve(DocumentDataSource source)
    {
        var culture = CultureFor(source.Language);
        var currency = source.Currency;

        var client = source.Client;
        var second = source.SecondDriver;
        var car = source.Car;
        var agency = source.Agency;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DocumentPlaceholders.ClientFullName] = Text(client?.FullName),
            [DocumentPlaceholders.ClientFirstName] = Text(client?.FirstName),
            [DocumentPlaceholders.ClientLastName] = Text(client?.LastName),
            [DocumentPlaceholders.ClientBirthDate] = Date(client?.BirthDate, culture),
            [DocumentPlaceholders.ClientBirthPlace] = Text(client?.BirthPlace),
            [DocumentPlaceholders.ClientCin] = Text(client?.CIN),
            [DocumentPlaceholders.ClientCinDeliveranceDate] = Date(client?.CINDeliveranceDate, culture),
            [DocumentPlaceholders.ClientCinDeliverancePlace] = Text(client?.CINDeliverancePlace),
            [DocumentPlaceholders.ClientPasseportNumber] = Text(client?.PasseportNumber),
            [DocumentPlaceholders.ClientDrivingLicenceNumber] = Text(client?.DrivingLicenceNumber),
            [DocumentPlaceholders.ClientDrivingLicenceDate] = Date(client?.DrivingLicenceDeliveranceDate, culture),
            [DocumentPlaceholders.ClientDescription] = Text(client?.Description),

            [DocumentPlaceholders.SecondDriverFullName] = Text(second?.FullName),
            [DocumentPlaceholders.SecondDriverBirthDate] = Date(second?.BirthDate, culture),
            [DocumentPlaceholders.SecondDriverCin] = Text(second?.CIN),
            [DocumentPlaceholders.SecondDriverDrivingLicenceNumber] = Text(second?.DrivingLicenceNumber),

            [DocumentPlaceholders.CarModel] = Text(car?.Model),
            [DocumentPlaceholders.CarMatricule] = Text(car?.Matricule),
            [DocumentPlaceholders.CarColor] = Text(car?.Color),
            [DocumentPlaceholders.CarPower] = Number(car?.Power, culture),
            [DocumentPlaceholders.CarFuelType] = Text(car?.FuelType),

            [DocumentPlaceholders.RentingStartDate] = Date(source.StartDate, culture),
            [DocumentPlaceholders.RentingEndDate] = Date(source.EndDate, culture),
            [DocumentPlaceholders.RentingDays] = Number(BilledDays(source.StartDate, source.EndDate), culture),
            [DocumentPlaceholders.RentingStartMileage] = Number(source.StartMileage, culture),
            [DocumentPlaceholders.RentingPrice] = Amount(source.Price, currency, culture),
            [DocumentPlaceholders.RentingDeposit] = Amount(source.DepositAmount, currency, culture),
            [DocumentPlaceholders.RentingNotes] = Text(source.Notes),

            [DocumentPlaceholders.AgencyName] = Text(agency?.Name),
            [DocumentPlaceholders.AgencyAddress] = Text(agency?.Address),
            [DocumentPlaceholders.AgencyPhone] = Text(agency?.PhoneNumber),
            [DocumentPlaceholders.AgencyEmail] = Text(agency?.Email),

            [DocumentPlaceholders.DocumentNumber] = source.Number,
            [DocumentPlaceholders.DocumentIssuedAt] = Date(source.IssuedAt, culture),
            [DocumentPlaceholders.DocumentCurrency] = currency,

            [DocumentPlaceholders.FactureRentalAmount] = Amount(source.RentalAmount, currency, culture),
            [DocumentPlaceholders.FactureExtrasAmount] = Amount(source.ExtraServicesAmount, currency, culture),
            [DocumentPlaceholders.FactureTotal] = Amount(source.Total, currency, culture),
            [DocumentPlaceholders.FactureAmountPaid] = Amount(source.AmountPaid, currency, culture),
            [DocumentPlaceholders.FactureBalanceDue] = Amount(source.BalanceDue, currency, culture),
        };

        return values;
    }

    /// <summary>
    /// Formats a money amount the way the resolver does, for the invoice rows and
    /// totals the renderer receives alongside the blocks — so a line item and a
    /// {{facture.total}} placeholder never disagree about formatting.
    /// </summary>
    public static string FormatAmount(decimal? value, string currency, string language) =>
        Amount(value, currency, CultureFor(language));

    /// <summary>
    /// The document's own culture. Unknown or blank tags fall back to the
    /// invariant culture rather than throwing — a document must still render.
    /// </summary>
    public static CultureInfo CultureFor(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    // Billed days on the same half-open basis the pricing service uses, so a
    // {{renting.days}} on the paperwork matches what the customer was charged for.
    private static int? BilledDays(DateTime? start, DateTime? end)
    {
        if (start is not DateTime from || end is not DateTime to || to <= from)
        {
            return null;
        }

        return (int)Math.Ceiling((to - from).TotalDays);
    }

    private static string Text(string? value) => value?.Trim() ?? string.Empty;

    private static string Date(DateTime? value, CultureInfo culture) =>
        value?.ToString("d", culture) ?? string.Empty;

    private static string Number(int? value, CultureInfo culture) =>
        value?.ToString("N0", culture) ?? string.Empty;

    private static string Amount(decimal? value, string currency, CultureInfo culture) =>
        value is decimal amount ? $"{amount.ToString("N2", culture)} {currency}" : string.Empty;
}
