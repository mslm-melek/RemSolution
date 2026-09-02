using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Car.Commands.CreateCarUnavailabilityCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarUnavailabilityCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarUnavailabilityCommand;
using RemSolution.Application.Features.Car.Queries.GetCarUnavailabilitiesQuery;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

/// <summary>
/// Time declared off the road. The point of the feature is the last two tests:
/// a car booked into the garage on the 20th cannot also be hired out on the 20th,
/// in either order.
/// </summary>
public class CarUnavailabilityTests : BaseTestFixture
{
    private static readonly DateTime From = new(2030, 5, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2030, 5, 24, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldDeclareABlock()
    {
        var userId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        var carId = await SeedBookableCarAsync();

        var id = await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId,
            StartDate = From,
            EndDate = To,
            Reason = CarUnavailabilityReason.Maintenance,
            Note = "Timing belt"
        });

        var block = await FindAsync<CarUnavailability>(id);

        block.Should().NotBeNull();
        block!.AgencyId.Should().Be(agencyId);
        block.CreatedBy.Should().Be(userId);
        block.CarId.Should().Be(carId);
        block.StartDate.Should().Be(From);
        block.EndDate.Should().Be(To);
        block.Reason.Should().Be(CarUnavailabilityReason.Maintenance);
        block.Note.Should().Be("Timing belt");
    }

    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateCarUnavailabilityCommand()))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectAPeriodThatEndsBeforeItStarts()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await SeedBookableCarAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = To, EndDate = From
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectABlockForAnotherAgencysCar()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = 999_999, StartDate = From, EndDate = To
        })).Should().ThrowAsync<Ardalis.GuardClauses.NotFoundException>();
    }

    /// <summary>
    /// The rule the whole table exists for: a hire cannot land on days the car is
    /// already booked into the garage.
    /// </summary>
    [Test]
    public async Task ShouldBlockAHireOverTheDeclaredPeriod()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From, EndDate = To, Reason = CarUnavailabilityReason.Maintenance
        });

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            // Straddles the start of the block.
            StartDate = From.AddDays(-1),
            EndDate = From.AddDays(1)
        })).Should().ThrowAsync<BookingConflictException>();
    }

    [Test]
    public async Task ShouldBlockAHoldOverTheDeclaredPeriod()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From, EndDate = To
        });

        await FluentActions.Invoking(() => SendAsync(new CreateReservationCommand
        {
            CarId = carId, ClientId = clientId, StartDate = From.AddDays(1), EndDate = From.AddDays(2)
        })).Should().ThrowAsync<BookingConflictException>();
    }

    /// <summary>
    /// The end date is exclusive, so a hire starting the day the car is back is
    /// not a conflict. This is the case a half-open bug would break.
    /// </summary>
    [Test]
    public async Task ShouldAllowAHireStartingTheDayTheCarIsBack()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From, EndDate = To
        });

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = To, EndDate = To.AddDays(2)
        });

        id.Should().BeGreaterThan(0);
    }

    /// <summary>And the other way round: the car is already out.</summary>
    [Test]
    public async Task ShouldRefuseABlockOverAnExistingHire()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = From, EndDate = To
        });

        await FluentActions.Invoking(() => SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From.AddDays(1), EndDate = To.AddDays(1)
        })).Should().ThrowAsync<BookingConflictException>();
    }

    [Test]
    public async Task ShouldAmendABlockWithoutCollidingWithItself()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await SeedBookableCarAsync();

        var id = await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From, EndDate = To
        });

        // Overlaps its own old period — the exclusion is what makes this work.
        await SendAsync(new UpdateCarUnavailabilityCommand
        {
            Id = id,
            StartDate = From.AddDays(1),
            EndDate = To.AddDays(2),
            Reason = CarUnavailabilityReason.Repair,
            Note = "Gearbox"
        });

        var block = await FindAsync<CarUnavailability>(id);

        block!.StartDate.Should().Be(From.AddDays(1));
        block.EndDate.Should().Be(To.AddDays(2));
        block.Reason.Should().Be(CarUnavailabilityReason.Repair);
    }

    [Test]
    public async Task ShouldDeleteABlockAndFreeTheDates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync();
        var clientId = await SeedClientAsync();

        var id = await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From, EndDate = To
        });

        await SendAsync(new DeleteCarUnavailabilityCommand(id));

        (await FindAsync<CarUnavailability>(id)).Should().BeNull();

        // And the dates are bookable again.
        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = From, EndDate = To
        });

        rentingId.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ShouldListOnlyBlocksThatAreNotOver()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await SeedBookableCarAsync();

        // Written straight to the database: the create command would refuse a
        // period in the past only if it overlapped something, but going through
        // it would also make the test depend on today's date twice.
        await AddAsync(CarUnavailability.Create(
            carId, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 1, 5, 0, 0, 0, DateTimeKind.Utc), CarUnavailabilityReason.Repair));

        await SendAsync(new CreateCarUnavailabilityCommand
        {
            CarId = carId, StartDate = From, EndDate = To
        });

        var upcoming = await SendAsync(new GetCarUnavailabilitiesQuery(carId));
        upcoming.Should().HaveCount(1);
        upcoming[0].StartDate.Should().Be(From);

        var all = await SendAsync(new GetCarUnavailabilitiesQuery(carId, IncludePast: true));
        all.Should().HaveCount(2);
    }

    private static async Task<int> SeedBookableCarAsync()
    {
        var car = new Car
        {
            Matricule = "BLOCK-1",
            Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"),
        };

        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync()
    {
        var client = new Client { FirstName = "Block", LastName = "Client" };
        await AddAsync(client);
        return client.Id;
    }
}
