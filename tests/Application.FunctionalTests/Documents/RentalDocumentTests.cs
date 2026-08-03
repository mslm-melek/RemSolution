using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Contract.Commands.GenerateContractCommand;
using RemSolution.Application.Features.Contract.Queries.GetContractsByRentingQuery;
using RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand;
using RemSolution.Application.Features.Facture.Queries.GetFacturesByRentingQuery;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Documents;

using static Testing;

/// <summary>
/// Generated paperwork: the numbering invariant, the on-save shortcut from the
/// booking screen, and the permission/feature pair each document is gated on.
/// </summary>
public class RentalDocumentTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 5, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldGenerateAContractWithANumberAndAnArchivedPdf()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();

        var contract = await SendAsync(new GenerateContractCommand { RentingId = rentingId });

        contract.RentingId.Should().Be(rentingId);
        contract.Number.Should().Be($"CTR-{DateTime.UtcNow.Year}-000001");
        contract.DocumentUrl.Should().NotBeNullOrWhiteSpace();
        contract.DocumentSize.Should().BeGreaterThan(0);

        var stored = await FindAsync<Contract>(contract.Id);
        stored.Should().NotBeNull();
        stored!.AgencyId.Should().Be(agencyId);
        stored.SequenceNumber.Should().Be(1);

        // The PDF really landed in storage, not just a row claiming it did.
        var file = await FindAsync<StoredFile>(stored.DocumentFileId);
        file!.DocumentType.Should().Be(DocumentType.RentalContract);
        file.MimeType.Should().Be("application/pdf");
        File.Exists(Path.Combine(UploadsRoot, file.Path.Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeTrue("the rendered bytes must be written under the storage root");
    }

    /// <summary>
    /// Regenerating issues the NEXT number rather than replacing the previous
    /// document — the copy the client already signed has to stay retrievable.
    /// </summary>
    [Test]
    public async Task ShouldNumberEachGenerationSequentially()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();

        var first = await SendAsync(new GenerateContractCommand { RentingId = rentingId });
        var second = await SendAsync(new GenerateContractCommand { RentingId = rentingId });

        second.Number.Should().NotBe(first.Number);
        second.Id.Should().NotBe(first.Id);

        var contracts = await SendAsync(new GetContractsByRentingQuery(rentingId));
        contracts.Should().HaveCount(2);
        // Newest first, so the agent hands over the current agreement.
        contracts[0].Number.Should().Be(second.Number);
    }

    /// <summary>
    /// The sequence is scoped to the agency: two tenants both start at 1 and
    /// neither can see or shift the other's numbering.
    /// </summary>
    [Test]
    public async Task ShouldNumberIndependentlyPerAgency()
    {
        await RunAsAgencyAdministratorAsync();

        await AddTestAgencyAsync();
        var firstAgencyRenting = await SeedRentingAsync("DOC-A");
        var a = await SendAsync(new GenerateContractCommand { RentingId = firstAgencyRenting });

        await AddTestAgencyAsync();
        var secondAgencyRenting = await SeedRentingAsync("DOC-B");
        var b = await SendAsync(new GenerateContractCommand { RentingId = secondAgencyRenting });

        a.Number.Should().Be(b.Number, "each agency runs its own sequence");
    }

    [Test]
    public async Task ShouldInvoiceTheRentalPlusExtraServicesNetOfPayments()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // 3 billed days × 50 = 150.
        var rentingId = await SeedRentingAsync();

        var type = new ExtraServicesType { Name = "Baby seat", IsActive = true };
        await AddAsync(type);
        await AddAsync(new ExtraService
        {
            RentingId = rentingId,
            ExtraServicesTypeId = type.Id,
            TotalAmount = Money.Of(40m, "TND")
        });

        await AddAsync(new Payment
        {
            RentingId = rentingId,
            PayementAmount = Money.Of(100m, "TND"),
            PayementDate = DateTime.UtcNow,
            Method = PaymentMethod.Cash
        });

        var facture = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        facture.RentalAmount!.Amount.Should().Be(150m);
        facture.ExtraServicesAmount!.Amount.Should().Be(40m);
        facture.TotalAmount!.Amount.Should().Be(190m);
        facture.TotalAmount.Currency.Should().Be("TND");

        var stored = await FindAsync<Facture>(facture.Id);
        stored!.Number.Should().Be($"FAC-{DateTime.UtcNow.Year}-000001");

        var file = await FindAsync<StoredFile>(stored.DocumentFileId);
        file!.DocumentType.Should().Be(DocumentType.RentalFacture);
    }

    /// <summary>
    /// An invoice is a snapshot: later changes to the renting must not rewrite
    /// what an issued invoice says.
    /// </summary>
    [Test]
    public async Task ShouldNotRewriteAnIssuedInvoiceWhenTheRentingChanges()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();
        var facture = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        var type = new ExtraServicesType { Name = "GPS", IsActive = true };
        await AddAsync(type);
        await AddAsync(new ExtraService
        {
            RentingId = rentingId,
            ExtraServicesTypeId = type.Id,
            TotalAmount = Money.Of(25m, "TND")
        });

        var stored = await FindAsync<Facture>(facture.Id);
        stored!.TotalAmount!.Amount.Should().Be(150m, "the extra service was added after the invoice was issued");
    }

    // ---- the on-save shortcut from the booking screen ----

    [Test]
    public async Task ShouldIssueBothDocumentsWithTheRentingWhenAsked()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync("DOC-BOTH");

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Counter", LastName = "Customer", BirthDate = new DateTime(1987, 2, 3)
            },
            StartDate = Start,
            EndDate = End,
            GenerateContract = true,
            GenerateFacture = true
        });

        (await SendAsync(new GetContractsByRentingQuery(rentingId))).Should().HaveCount(1);
        (await SendAsync(new GetFacturesByRentingQuery(rentingId))).Should().HaveCount(1);
    }

    [Test]
    public async Task ShouldIssueNoDocumentsWhenNeitherIsAsked()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();

        (await CountAsync<Contract>()).Should().Be(0);
        (await CountAsync<Facture>()).Should().Be(0);
        (await SendAsync(new GetContractsByRentingQuery(rentingId))).Should().BeEmpty();
    }

    /// <summary>
    /// Asking for a document the user may not issue fails the WHOLE save: the
    /// agent asked for a contract, and a renting without one is not what they
    /// requested.
    /// </summary>
    [Test]
    public async Task ShouldRollBackTheRentingWhenTheRequestedContractIsNotPermitted()
    {
        await RunAsAgencyStaffAsync(Permissions.RentingCreate, Permissions.ClientCreate);
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync("DOC-DENIED");

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Rolled", LastName = "Back", BirthDate = new DateTime(1987, 2, 3)
            },
            StartDate = Start,
            EndDate = End,
            GenerateContract = true
        })).Should().ThrowAsync<ForbiddenAccessException>();

        (await CountAsync<Renting>()).Should().Be(0);
        (await CountAsync<Contract>()).Should().Be(0);
        (await CountAsync<Client>(c => c.FirstName == "Rolled")).Should().Be(0);
    }

    [Test]
    public async Task ShouldRollBackTheRentingWhenTheContractsFeatureIsDisabled()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Contracts, Enabled = false });

        var carId = await SeedBookableCarAsync("DOC-NOFEAT");
        var clientId = await SeedClientAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            StartDate = Start,
            EndDate = End,
            GenerateContract = true
        })).Should().ThrowAsync<ForbiddenAccessException>();

        (await CountAsync<Renting>()).Should().Be(0);
    }

    // ---- gating on the standalone commands ----

    [Test]
    public async Task ShouldRefuseGeneratingAContractWithoutThePermission()
    {
        // Booking is allowed (the seed needs it); issuing the agreement is not.
        await RunAsAgencyStaffAsync(Permissions.RentingCreate);
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand { RentingId = rentingId }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldRefuseGeneratingAContractWhenTheFeatureIsDisabled()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Contracts, Enabled = false });

        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand { RentingId = rentingId }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldRefuseReadingContractsWithoutTheReadPermission()
    {
        await RunAsAgencyStaffAsync(Permissions.RentingCreate);
        await AddTestAgencyAsync();

        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GetContractsByRentingQuery(rentingId)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    /// <summary>
    /// Contracts and invoices are separate modules: an agency that sells one
    /// without the other gets exactly that.
    /// </summary>
    [Test]
    public async Task ShouldGateInvoicesIndependentlyOfContracts()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Factures, Enabled = false });

        var rentingId = await SeedRentingAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateFactureCommand { RentingId = rentingId }))
            .Should().ThrowAsync<ForbiddenAccessException>();

        (await SendAsync(new GenerateContractCommand { RentingId = rentingId })).Number.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task ShouldRejectAnUnknownRenting()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new GenerateContractCommand { RentingId = 9999 }))
            .Should().ThrowAsync<NotFoundException>();
    }

    private static async Task<int> SeedRentingAsync(string matricule = "DOC-1")
    {
        var carId = await SeedBookableCarAsync(matricule);
        var clientId = await SeedClientAsync();

        return await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });
    }

    private static async Task<int> SeedBookableCarAsync(string matricule)
    {
        var car = new Car
        {
            Matricule = matricule,
            Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"),
        };
        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync()
    {
        var client = new Client
        {
            FirstName = "Document",
            LastName = "Client",
            BirthDate = new DateTime(1990, 1, 1),
            CIN = "09000111"
        };
        await AddAsync(client);
        return client.Id;
    }
}
