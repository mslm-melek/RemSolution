using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Jobs;

namespace RemSolution.Application.FunctionalTests.Clients;

using static Testing;

public class PersonalDataPurgeJobTests : BaseTestFixture
{
    private static Task RunJobAsync() =>
        UsingScopeAsync(async sp =>
        {
            await sp.GetRequiredService<PersonalDataPurgeJob>().RunAsync();
            return true;
        });

    private static Task SetRetentionAsync(int agencyId, int months) =>
        UsingScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<RemSolution.Infrastructure.Data.ApplicationDbContext>();
            var settings = context.AgencySettings.Single(s => s.AgencyId == agencyId);
            settings.PersonalDataRetentionMonths = months;
            await context.SaveChangesAsync(CancellationToken.None);
            return true;
        });

    private static async Task<Client> AddClientAsync(string cin, DateTimeOffset createdOn)
    {
        var client = new Client
        {
            FirstName = "Old",
            LastName = "Customer",
            CIN = cin,
            CreatedOn = createdOn
        };

        await AddAsync(client);

        // The audit interceptor stamps CreatedOn on insert, so it is overwritten
        // afterwards: "last dealing" is what this job reads, and a test cannot
        // wait three years for it.
        await UsingScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<RemSolution.Infrastructure.Data.ApplicationDbContext>();
            var row = context.Clients.Single(c => c.Id == client.Id);
            row.CreatedOn = createdOn;
            await context.SaveChangesAsync(CancellationToken.None);
            return true;
        });

        return client;
    }

    private static async Task<Car> AddCarAsync(string matricule)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);
        return car;
    }

    [Test]
    public async Task DoesNothingForAnAgencyThatHasNotSetAWindow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var client = await AddClientAsync("11111111", DateTimeOffset.UtcNow.AddYears(-9));

        await RunJobAsync();

        // Zero is the default, and it means "no automatic purge" — an agency has
        // to choose a retention period, we do not invent one.
        (await FindAsync<Client>(client.Id))!.PersonalDataErasedAt.Should().BeNull();
    }

    [Test]
    public async Task ErasesAClientWhoseLastDealingIsOlderThanTheWindow()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await SetRetentionAsync(agencyId, months: 24);

        var stale = await AddClientAsync("22222222", DateTimeOffset.UtcNow.AddYears(-5));
        var recent = await AddClientAsync("33333333", DateTimeOffset.UtcNow.AddMonths(-2));

        await RunJobAsync();

        var erased = await FindAsync<Client>(stale.Id);
        erased!.PersonalDataErasedAt.Should().NotBeNull();
        erased.CIN.Should().BeNull();
        erased.LastName.Should().Be($"#{stale.Id}");

        (await FindAsync<Client>(recent.Id))!.CIN.Should().Be("33333333");
    }

    [Test]
    public async Task ARecentHireKeepsAnOldClientRecordAlive()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await SetRetentionAsync(agencyId, months: 12);

        // The row was typed in years ago, but this customer came back last month:
        // the window runs from the last dealing, not from the record's birthday.
        var client = await AddClientAsync("44444444", DateTimeOffset.UtcNow.AddYears(-6));
        var car = await AddCarAsync("PJ-1");

        await AddAsync(RentingFixture.Hire(
            car.Id, client.Id,
            DateTime.UtcNow.Date.AddMonths(-1), DateTime.UtcNow.Date.AddMonths(-1).AddDays(4),
            RentingState.Done, price: Money.Of(320m, "TND")));

        await RunJobAsync();

        (await FindAsync<Client>(client.Id))!.CIN.Should().Be("44444444");
    }

    [Test]
    public async Task CountsBeingTheSecondDriverAsADealing()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await SetRetentionAsync(agencyId, months: 12);

        var renter = await AddClientAsync("55555555", DateTimeOffset.UtcNow.AddYears(-6));
        var second = await AddClientAsync("66666666", DateTimeOffset.UtcNow.AddYears(-6));
        var car = await AddCarAsync("PJ-2");

        await AddAsync(RentingFixture.Hire(
            car.Id, renter.Id,
            DateTime.UtcNow.Date.AddDays(-20), DateTime.UtcNow.Date.AddDays(-16),
            RentingState.Done, price: Money.Of(300m, "TND"),
            secondClientId: second.Id));

        await RunJobAsync();

        // A second driver handed over their licence too, so the clock runs for
        // them from the same hire.
        (await FindAsync<Client>(second.Id))!.CIN.Should().Be("66666666");
    }

    [Test]
    public async Task SkipsAClientWithABookingStillOpen()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await SetRetentionAsync(agencyId, months: 1);

        var client = await AddClientAsync("77777777", DateTimeOffset.UtcNow.AddYears(-4));
        var car = await AddCarAsync("PJ-3");

        // Dated long ago but still in progress — the car is out, and there has to
        // be somebody to hand it back to.
        await AddAsync(RentingFixture.Hire(
            car.Id, client.Id,
            DateTime.UtcNow.Date.AddYears(-3), DateTime.UtcNow.Date.AddYears(-3).AddDays(2),
            RentingState.InProgress, price: Money.Of(200m, "TND")));

        await RunJobAsync();

        (await FindAsync<Client>(client.Id))!.PersonalDataErasedAt.Should().BeNull();
    }

    [Test]
    public async Task ReachesArchivedClientsToo()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await SetRetentionAsync(agencyId, months: 12);

        var client = await AddClientAsync("88888888", DateTimeOffset.UtcNow.AddYears(-4));
        await SendAsync(new DeleteClientCommand(client.Id));

        await RunJobAsync();

        // The whole point: an archived client is the one whose passport scan
        // nobody will ever look at again.
        var archived = await FindIgnoringFiltersAsync<Client>(c => c.Id == client.Id);
        archived!.IsDeleted.Should().BeTrue();
        archived.PersonalDataErasedAt.Should().NotBeNull();
        archived.CIN.Should().BeNull();
    }

    [Test]
    public async Task IsSafeToRunTwice()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await SetRetentionAsync(agencyId, months: 12);

        var client = await AddClientAsync("99999999", DateTimeOffset.UtcNow.AddYears(-4));

        await RunJobAsync();
        var erasedAt = (await FindAsync<Client>(client.Id))!.PersonalDataErasedAt;

        await RunJobAsync();

        (await FindAsync<Client>(client.Id))!.PersonalDataErasedAt.Should().Be(erasedAt);
    }
}
