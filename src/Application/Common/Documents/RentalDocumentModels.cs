namespace RemSolution.Application.Common.Documents;

/// <summary>The lessor, as printed.</summary>
public sealed record RentalDocumentAgency(
    string Name,
    string? Address,
    string? PhoneNumber,
    string? Email);

/// <summary>A party on the document — the renter or the additional driver.</summary>
public sealed record RentalDocumentParty(
    string FullName,
    string? FirstName,
    string? LastName,
    DateTime? BirthDate,
    string? BirthPlace,
    string? CIN,
    DateTime? CINDeliveranceDate,
    string? CINDeliverancePlace,
    string? PasseportNumber,
    string? DrivingLicenceNumber,
    DateTime? DrivingLicenceDeliveranceDate,
    string? Description);

public sealed record RentalDocumentCar(
    string? Model,
    string? Matricule,
    string? Color,
    int? Power,
    string? FuelType);

/// <summary>
/// Everything a template may pull from, in one flat record: the source side of
/// <see cref="RemSolution.Domain.Constants.DocumentPlaceholders"/>.
/// <para>
/// Values are still typed here — dates are dates, amounts are decimals — because
/// formatting them is the resolver's job and it needs the culture to do it. By
/// the time anything reaches the renderer it is a string.
/// </para>
/// </summary>
public sealed record DocumentDataSource
{
    public required string Language { get; init; }
    public required string Currency { get; init; }

    /// <summary>The number this document is being issued under.</summary>
    public required string Number { get; init; }

    public required DateTime IssuedAt { get; init; }

    public RentalDocumentAgency? Agency { get; init; }
    public RentalDocumentParty? Client { get; init; }

    /// <summary>Null unless the booking names an additional authorised driver.</summary>
    public RentalDocumentParty? SecondDriver { get; init; }

    public RentalDocumentCar? Car { get; init; }

    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? StartMileage { get; init; }
    public decimal? Price { get; init; }
    public decimal? DepositAmount { get; init; }
    public string? Notes { get; init; }

    /// <summary>Invoice totals; all null on a contract, which has nothing to total.</summary>
    public decimal? RentalAmount { get; init; }
    public decimal? ExtraServicesAmount { get; init; }
    public decimal? Total { get; init; }
    public decimal? AmountPaid { get; init; }
    public decimal? BalanceDue { get; init; }
}
