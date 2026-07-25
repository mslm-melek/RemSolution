using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Commands.UpdateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

public class UpdateRentingTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 2, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task EditingNonDateFieldsShouldPreserveThePriceSnapshot()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "UPD-1", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        // 3 days × 50 = 150.

        // The car's rate changes after booking — the contract price must not move.
        car.DailyRate = Money.Of(999m, "TND");
        await UpdateAsync(car);

        await SendAsync(new UpdateRentingCommand
        {
            Id = id,
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = Start,
            EndDate = End,
            Notes = "just editing a note"
        });

        var renting = await FindAsync<Renting>(id);
        renting!.Price!.Amount.Should().Be(150m);
    }

    [Test]
    public async Task ChangingDatesShouldRequoteThePrice()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "UPD-2", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        // Extend by two days → 5 × 50 = 250.
        await SendAsync(new UpdateRentingCommand
        {
            Id = id,
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = Start,
            EndDate = End.AddDays(2)
        });

        var renting = await FindAsync<Renting>(id);
        renting!.Price!.Amount.Should().Be(250m);
    }
}
