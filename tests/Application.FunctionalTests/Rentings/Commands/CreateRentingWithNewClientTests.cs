using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

/// <summary>
/// The counter flow: an agent books a walk-in customer who is not in the system
/// yet, from the booking screen. The point of doing it in one command is that a
/// failed booking must not leave a client behind, so most of these tests are
/// about what happens when the save does NOT succeed.
/// </summary>
public class CreateRentingWithNewClientTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 3, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldCreateTheClientAndTheRentingTogether()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Walk",
                LastName = "In",
                BirthDate = new DateTime(1988, 6, 2),
                CIN = "09112233",
                DrivingLicenceNumber = "12-998877"
            },
            StartDate = Start,
            EndDate = End
        });

        var renting = await FindAsync<Renting>(rentingId);
        renting.Should().NotBeNull();
        renting!.ClientId.Should().NotBeNull();

        var client = await FindAsync<Client>(renting.ClientId!.Value);
        client.Should().NotBeNull();
        client!.FirstName.Should().Be("Walk");
        client.CIN.Should().Be("09112233");
        // Stamped from the tenant, never accepted from the caller.
        client.AgencyId.Should().Be(agencyId);
    }

    /// <summary>
    /// The whole reason this is one command: a car that turns out to be taken
    /// must roll the new client back with the renting.
    /// </summary>
    [Test]
    public async Task ShouldRollBackTheNewClientWhenTheCarIsUnavailable()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();
        var existingClientId = await SeedClientAsync();

        await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = existingClientId, StartDate = Start, EndDate = End
        });

        var clientsBefore = await CountAsync<Client>();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Never",
                LastName = "Created",
                BirthDate = new DateTime(1991, 1, 1)
            },
            StartDate = Start.AddDays(1),
            EndDate = End.AddDays(1)
        })).Should().ThrowAsync<BookingConflictException>();

        // Soft-deleted rows are filtered out of this count, so also check nothing
        // was archived rather than rolled back.
        (await CountAsync<Client>()).Should().Be(clientsBefore);
        (await CountAsync<Client>(c => c.FirstName == "Never")).Should().Be(0);
    }

    [Test]
    public async Task ShouldRejectSupplyingBothAnExistingAndANewClient()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            NewClient = new NewRentingClient
            {
                FirstName = "Both", LastName = "Supplied", BirthDate = new DateTime(1990, 1, 1)
            },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectSupplyingNeitherClient()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId, StartDate = Start, EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// The inline payload goes through the same identity-document rules as the
    /// standalone client commands — a birth date is required, so a payload
    /// without one is a validation failure rather than a half-filled client.
    /// </summary>
    [Test]
    public async Task ShouldApplyTheSharedClientRulesToTheInlinePayload()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient { FirstName = "No", LastName = "Birthdate" },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// The dedup rule from the reservation-conversion path: a walk-in whose CIN
    /// the agency already holds is the SAME person, so the renting links to the
    /// existing record instead of adding a second one for them.
    /// </summary>
    [Test]
    public async Task ShouldReuseAnExistingClientCarryingTheSameCin()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();

        var existing = new Client { FirstName = "Known", LastName = "Customer", CIN = "09445566" };
        await AddAsync(existing);

        var clientsBefore = await CountAsync<Client>();

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Known",
                LastName = "Customer",
                BirthDate = new DateTime(1985, 9, 9),
                CIN = "09445566",
                DrivingLicenceNumber = "12-111222"
            },
            StartDate = Start,
            EndDate = End
        });

        (await CountAsync<Client>()).Should().Be(clientsBefore, "the CIN already identifies a client of this agency");

        var renting = await FindAsync<Renting>(rentingId);
        renting!.ClientId.Should().Be(existing.Id);

        // Blanks are filled in from what the agent typed; what was already
        // recorded is left alone.
        var reused = await FindAsync<Client>(existing.Id);
        reused!.DrivingLicenceNumber.Should().Be("12-111222");
        reused.FirstName.Should().Be("Known");
    }

    /// <summary>
    /// Creating a renting and creating a client are separate permissions. Staff
    /// who may book but not add customers can still pick an existing one.
    /// </summary>
    [Test]
    public async Task ShouldRefuseTheInlineClientWithoutTheClientCreatePermission()
    {
        await RunAsAgencyStaffAsync(Permissions.RentingCreate);
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Not", LastName = "Allowed", BirthDate = new DateTime(1990, 1, 1)
            },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ForbiddenAccessException>();

        (await CountAsync<Client>(c => c.FirstName == "Not")).Should().Be(0);
    }

    [Test]
    public async Task ShouldAllowPickingAnExistingClientWithoutTheClientCreatePermission()
    {
        await RunAsAgencyStaffAsync(Permissions.RentingCreate);
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });

        (await FindAsync<Renting>(rentingId)).Should().NotBeNull();
    }

    /// <summary>
    /// A switched-off Clients module blocks the inline path too — otherwise the
    /// booking screen would be a way around the module gate.
    /// </summary>
    [Test]
    public async Task ShouldRefuseTheInlineClientWhenTheClientsFeatureIsDisabled()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Clients, Enabled = false });

        var carId = await SeedBookableCarAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Feature", LastName = "Off", BirthDate = new DateTime(1990, 1, 1)
            },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }

    /// <summary>
    /// The plan's client quota applies wherever a client is created, not just on
    /// the Clients screen.
    /// </summary>
    [Test]
    public async Task ShouldEnforceTheClientQuotaOnTheInlinePath()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync(maxClients: 1);

        var carId = await SeedBookableCarAsync();
        await SeedClientAsync(); // fills the quota

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            NewClient = new NewRentingClient
            {
                FirstName = "Over", LastName = "Quota", BirthDate = new DateTime(1990, 1, 1)
            },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<PlanLimitExceededException>();
    }

    private static async Task<int> SeedBookableCarAsync()
    {
        var car = new Car
        {
            Matricule = "INLINE-1",
            Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"),
        };
        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync()
    {
        var client = new Client { FirstName = "Existing", LastName = "Client" };
        await AddAsync(client);
        return client.Id;
    }
}
