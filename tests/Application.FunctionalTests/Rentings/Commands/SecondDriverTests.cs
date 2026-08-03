using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Commands.UpdateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

/// <summary>
/// The second driver, who is as likely to be a walk-in as the renter — a couple at
/// the counter, one of whom has never rented here. Both booking write paths accept
/// either a picked client or an inline one; the rules that only apply to the second
/// driver are "at most one source", "not the renter", and "none removes them".
/// </summary>
public class SecondDriverTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 10, 5, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldCreateTheSecondDriverInlineWithTheBooking()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-1");
        var renterId = await SeedClientAsync("Main", "Renter");

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = renterId,
            SecondNewClient = new NewRentingClient
            {
                FirstName = "Second",
                LastName = "Driver",
                BirthDate = new DateTime(1990, 4, 12),
                CIN = "77001122",
                DrivingLicenceNumber = "12-554433"
            },
            StartDate = Start,
            EndDate = End
        });

        var renting = await FindAsync<Renting>(rentingId);

        renting!.SecondClientId.Should().NotBeNull();
        renting.SecondClientId.Should().NotBe(renterId);

        var second = await FindAsync<Client>(renting.SecondClientId!.Value);
        second!.FirstName.Should().Be("Second");
        second.CIN.Should().Be("77001122");
    }

    [Test]
    public async Task ShouldRollBackAnInlineSecondDriverWhenTheCarIsTaken()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-2");
        var renterId = await SeedClientAsync("Main", "Renter");

        await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = renterId, StartDate = Start, EndDate = End
        });

        var before = await CountAsync<Client>();

        // Overlaps the booking above, so nothing may survive — including the
        // second driver created earlier in the same transaction.
        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = renterId,
            SecondNewClient = new NewRentingClient
            {
                FirstName = "Never",
                LastName = "Saved",
                BirthDate = new DateTime(1991, 1, 1)
            },
            StartDate = Start.AddDays(1),
            EndDate = End.AddDays(1)
        })).Should().ThrowAsync<BookingConflictException>();

        (await CountAsync<Client>()).Should().Be(before);
    }

    /// <summary>
    /// The dedup rule can land on the renter: typing their own CIN into the
    /// second-driver form would otherwise produce a booking whose two drivers are
    /// one row. Checked on the resolved rows, not the ids.
    /// </summary>
    [Test]
    public async Task ShouldRefuseASecondDriverWhoTurnsOutToBeTheRenter()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-3");

        var renter = new Client
        {
            FirstName = "Same", LastName = "Person", CIN = "88554433"
        };
        await AddAsync(renter);

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = renter.Id,
            // Same CIN → dedup resolves to the renter.
            SecondNewClient = new NewRentingClient
            {
                FirstName = "Same",
                LastName = "Person",
                BirthDate = new DateTime(1985, 2, 2),
                CIN = "88554433"
            },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRefuseBothSecondDriverSourcesAtOnce()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-4");
        var renterId = await SeedClientAsync("Main", "Renter");
        var otherId = await SeedClientAsync("Other", "Client");

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = renterId,
            SecondClientId = otherId,
            SecondNewClient = new NewRentingClient
            {
                FirstName = "Both", LastName = "Sources", BirthDate = new DateTime(1990, 1, 1)
            },
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldAddASecondDriverInlineToAnExistingBooking()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-5");
        var renterId = await SeedClientAsync("Main", "Renter");

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = renterId, StartDate = Start, EndDate = End
        });

        // The partner turns up the next day and will share the driving.
        await SendAsync(new UpdateRentingCommand
        {
            Id = rentingId,
            CarId = carId,
            ClientId = renterId,
            SecondNewClient = new NewRentingClient
            {
                FirstName = "Added",
                LastName = "Later",
                BirthDate = new DateTime(1993, 7, 7),
                DrivingLicenceNumber = "12-111222"
            },
            StartDate = Start,
            EndDate = End
        });

        var renting = await FindAsync<Renting>(rentingId);
        var second = await FindAsync<Client>(renting!.SecondClientId!.Value);

        second!.LastName.Should().Be("Later");
        // The period did not move and no price was typed, so the snapshot stands.
        renting.Price!.Amount.Should().Be(200m);
    }

    /// <summary>
    /// Creating a second driver inline needs a SaveChanges mid-transaction to get
    /// their key, which lands before the renting's own fields are assigned. This
    /// pins that it does not spend the concurrency token early: a stale RowVersion
    /// must still be refused, and the inline client must not survive the refusal.
    /// </summary>
    [Test]
    public async Task AStaleRowVersionShouldStillConflictAndKeepNoInlineClient()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-7");
        var renterId = await SeedClientAsync("Main", "Renter");

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = renterId, StartDate = Start, EndDate = End
        });

        var stale = (await FindAsync<Renting>(rentingId))!.RowVersion;

        // Another editor saves first and bumps the version.
        await SendAsync(new UpdateRentingCommand
        {
            Id = rentingId,
            RowVersion = stale,
            CarId = carId,
            ClientId = renterId,
            StartDate = Start,
            EndDate = End,
            Notes = "first editor"
        });

        var before = await CountAsync<Client>();

        await FluentActions.Invoking(() => SendAsync(new UpdateRentingCommand
        {
            Id = rentingId,
            RowVersion = stale,
            CarId = carId,
            ClientId = renterId,
            SecondNewClient = new NewRentingClient
            {
                FirstName = "Lost", LastName = "ToConflict", BirthDate = new DateTime(1994, 4, 4)
            },
            StartDate = Start,
            EndDate = End,
            Notes = "second editor"
        })).Should().ThrowAsync<DbUpdateConcurrencyException>();

        (await CountAsync<Client>()).Should().Be(before);
        (await FindAsync<Renting>(rentingId))!.Notes.Should().Be("first editor");
    }

    [Test]
    public async Task ShouldRemoveTheSecondDriverWhenNeitherSourceIsGiven()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("SEC-6");
        var renterId = await SeedClientAsync("Main", "Renter");
        var secondId = await SeedClientAsync("Second", "Driver");

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = renterId,
            SecondClientId = secondId,
            StartDate = Start,
            EndDate = End
        });

        (await FindAsync<Renting>(rentingId))!.SecondClientId.Should().Be(secondId);

        await SendAsync(new UpdateRentingCommand
        {
            Id = rentingId,
            CarId = carId,
            ClientId = renterId,
            StartDate = Start,
            EndDate = End
        });

        var renting = await FindAsync<Renting>(rentingId);

        renting!.SecondClientId.Should().BeNull();
        // Taken off the booking, not deleted — they are still the agency's client.
        (await FindAsync<Client>(secondId)).Should().NotBeNull();
    }

    private static async Task SetUpAgencyAsync()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
    }

    private static async Task<int> SeedCarAsync(string matricule)
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

    private static async Task<int> SeedClientAsync(string firstName, string lastName)
    {
        var client = new Client { FirstName = firstName, LastName = lastName };
        await AddAsync(client);
        return client.Id;
    }
}
