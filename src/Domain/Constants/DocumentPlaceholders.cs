using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Constants;

/// <summary>
/// Every value a document template can pull from a booking, as a stable dotted
/// path. This is the contract between a template and the data: the template
/// editor offers this list, auto-binding matches a placeholder name against it,
/// and the resolver in the Application layer answers exactly these paths.
/// <para>
/// Paths are part of the saved template, so RENAMING one breaks every template
/// that used it. Add new paths; retire old ones only with a data migration.
/// </para>
/// </summary>
public static class DocumentPlaceholders
{
    // Client / renter
    public const string ClientFullName = "client.fullName";
    public const string ClientFirstName = "client.firstName";
    public const string ClientLastName = "client.lastName";
    public const string ClientBirthDate = "client.birthDate";
    public const string ClientBirthPlace = "client.birthPlace";
    public const string ClientCin = "client.cin";
    public const string ClientCinDeliveranceDate = "client.cinDeliveranceDate";
    public const string ClientCinDeliverancePlace = "client.cinDeliverancePlace";
    public const string ClientPasseportNumber = "client.passeportNumber";
    public const string ClientDrivingLicenceNumber = "client.drivingLicenceNumber";
    public const string ClientDrivingLicenceDate = "client.drivingLicenceDeliveranceDate";
    public const string ClientDescription = "client.description";

    // Second authorised driver (blank when the booking names none)
    public const string SecondDriverFullName = "secondDriver.fullName";
    public const string SecondDriverBirthDate = "secondDriver.birthDate";
    public const string SecondDriverCin = "secondDriver.cin";
    public const string SecondDriverDrivingLicenceNumber = "secondDriver.drivingLicenceNumber";

    // Vehicle
    public const string CarModel = "car.model";
    public const string CarMatricule = "car.matricule";
    public const string CarColor = "car.color";
    public const string CarPower = "car.power";
    public const string CarFuelType = "car.fuelType";

    // Booking
    public const string RentingStartDate = "renting.startDate";
    public const string RentingEndDate = "renting.endDate";
    public const string RentingDays = "renting.days";
    public const string RentingStartMileage = "renting.startMileage";
    public const string RentingPrice = "renting.price";
    public const string RentingDeposit = "renting.deposit";
    public const string RentingNotes = "renting.notes";

    // Lessor
    public const string AgencyName = "agency.name";
    public const string AgencyAddress = "agency.address";
    public const string AgencyPhone = "agency.phoneNumber";
    public const string AgencyEmail = "agency.email";

    /// <summary>
    /// The agency's tax registration number ("matricule fiscal"). Shared rather
    /// than invoice-only: a contract may carry it too, and an agency that has not
    /// entered one gets a blank, not a placeholder.
    /// </summary>
    public const string AgencyTaxIdentifier = "agency.taxIdentifier";

    // The document itself
    public const string DocumentNumber = "document.number";
    public const string DocumentIssuedAt = "document.issuedAt";
    public const string DocumentCurrency = "document.currency";

    // Invoice-only totals: on a contract there is nothing yet to total.
    public const string FactureRentalAmount = "facture.rentalAmount";
    public const string FactureExtrasAmount = "facture.extraServicesAmount";

    /// <summary>Charges established at the return (see RentingFee).</summary>
    public const string FactureFeesAmount = "facture.feesAmount";
    /// <summary>Tax-inclusive total of the lines. See <see cref="FactureTotalDue"/>.</summary>
    public const string FactureTotal = "facture.total";
    public const string FactureAmountPaid = "facture.amountPaid";
    public const string FactureBalanceDue = "facture.balanceDue";

    // The tax breakdown a legal invoice has to print. Frozen on the Facture row
    // at issue, so a reprint years later still shows the rate that was charged.
    public const string FactureNetAmount = "facture.netAmount";
    public const string FactureVatRate = "facture.vatRate";
    public const string FactureVatAmount = "facture.vatAmount";
    public const string FactureFiscalStamp = "facture.fiscalStamp";

    /// <summary>Lines plus duty stamp — what the client owes.</summary>
    public const string FactureTotalDue = "facture.totalDue";

    private static readonly string[] Shared =
    {
        ClientFullName, ClientFirstName, ClientLastName, ClientBirthDate, ClientBirthPlace,
        ClientCin, ClientCinDeliveranceDate, ClientCinDeliverancePlace,
        ClientPasseportNumber, ClientDrivingLicenceNumber, ClientDrivingLicenceDate,
        ClientDescription,
        SecondDriverFullName, SecondDriverBirthDate, SecondDriverCin, SecondDriverDrivingLicenceNumber,
        CarModel, CarMatricule, CarColor, CarPower, CarFuelType,
        RentingStartDate, RentingEndDate, RentingDays, RentingStartMileage,
        RentingPrice, RentingDeposit, RentingNotes,
        AgencyName, AgencyAddress, AgencyPhone, AgencyEmail, AgencyTaxIdentifier,
        DocumentNumber, DocumentIssuedAt, DocumentCurrency,
    };

    private static readonly string[] FactureOnly =
    {
        FactureRentalAmount, FactureExtrasAmount, FactureFeesAmount,
        FactureTotal, FactureAmountPaid, FactureBalanceDue,
        FactureNetAmount, FactureVatRate, FactureVatAmount,
        FactureFiscalStamp, FactureTotalDue,
    };

    /// <summary>Every known path, regardless of document kind.</summary>
    public static readonly string[] All = Shared.Concat(FactureOnly).ToArray();

    /// <summary>
    /// The paths a template of this kind may bind to. A contract has no totals to
    /// print, so offering them would only invite empty values on paper.
    /// </summary>
    public static string[] For(DocumentTemplateKind kind) =>
        kind == DocumentTemplateKind.Facture ? All : Shared;

    public static bool IsKnown(string? path) =>
        path is not null && All.Contains(path);

    public static bool IsAvailableFor(string? path, DocumentTemplateKind kind) =>
        path is not null && For(kind).Contains(path);
}
