using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.UpdateMyAgencyCommand;
using RemSolution.Application.Features.Agency.Queries.GetMyAgencyQuery;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

// The agency administrator's self-service view of their own agency: the tenant
// comes from the caller's claim, so neither the query nor the command takes an
// agency id at all.
public class MyAgencyTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnTheCallersOwnAgency()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var agency = await SendAsync(new GetMyAgencyQuery());

        agency.Id.Should().Be(agencyId);
        agency.Name.Should().Be("Test Agency");
        agency.Currency.Should().Be("TND");
    }

    [Test]
    public async Task ShouldUpdateDetailsAndThePin()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var country = new Country { Name = "Newland" };
        await AddAsync(country);

        var before = await SendAsync(new GetMyAgencyQuery());

        await SendAsync(new UpdateMyAgencyCommand
        {
            RowVersion = before.RowVersion,
            Name = "Renamed Agency",
            Email = "contact@renamed.test",
            PhoneNumber = "+216 71 000 000",
            Address = "12 Avenue Habib Bourguiba, Tunis",
            Latitude = 36.8065,
            Longitude = 10.1815,
            CountryId = country.Id,
            CancellationWindowHours = 12,
            ReservationExpiryHours = 72
        });

        var agency = await FindAsync<Agency>(agencyId);

        agency!.Name.Should().Be("Renamed Agency");
        agency.Email.Should().Be("contact@renamed.test");
        agency.Address.Should().Be("12 Avenue Habib Bourguiba, Tunis");
        agency.CountryId.Should().Be(country.Id);
        agency.Location!.Y.Should().BeApproximately(36.8065, 1e-9);
        agency.Location.X.Should().BeApproximately(10.1815, 1e-9);
        agency.Location.SRID.Should().Be(4326);

        var settings = (await AllAsync<AgencySettings>()).Single(s => s.AgencyId == agencyId);
        settings.CancellationWindowHours.Should().Be(12);
        settings.ReservationExpiryHours.Should().Be(72);
    }

    // The currency is not on the command at all, so a self-service save must
    // leave it exactly as the platform administrator set it.
    [Test]
    public async Task ShouldNotTouchTheCurrency()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var before = await SendAsync(new GetMyAgencyQuery());

        await SendAsync(new UpdateMyAgencyCommand
        {
            RowVersion = before.RowVersion,
            Name = "Renamed Agency",
            CountryId = before.CountryId
        });

        var settings = (await AllAsync<AgencySettings>()).Single(s => s.AgencyId == agencyId);

        settings.CurrencyCode.Should().Be("TND");
    }

    [Test]
    public async Task ShouldRejectAHalfCoordinatePair()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var before = await SendAsync(new GetMyAgencyQuery());

        await FluentActions.Invoking(() => SendAsync(new UpdateMyAgencyCommand
        {
            RowVersion = before.RowVersion,
            Name = "Half located",
            CountryId = before.CountryId,
            Longitude = 10.1815
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectAStaleRowVersion()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var stale = await SendAsync(new GetMyAgencyQuery());

        // Someone else's save lands first.
        await SendAsync(new UpdateMyAgencyCommand
        {
            RowVersion = stale.RowVersion,
            Name = "First writer",
            CountryId = stale.CountryId
        });

        await FluentActions.Invoking(() => SendAsync(new UpdateMyAgencyCommand
        {
            RowVersion = stale.RowVersion,
            Name = "Second writer",
            CountryId = stale.CountryId
        })).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Test]
    public async Task ShouldBeForbiddenForAgencyStaff()
    {
        await RunAsDefaultUserAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new GetMyAgencyQuery())).Should().ThrowAsync<ForbiddenAccessException>();

        await FluentActions.Invoking(() => SendAsync(new UpdateMyAgencyCommand
        {
            Name = "Not mine to edit",
            CountryId = 1
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }

    // The platform administrator has no agency of their own; they edit an agency
    // through UpdateAgencyCommand, which names it.
    [Test]
    public async Task ShouldBeForbiddenForThePlatformAdministrator()
    {
        await RunAsPlatformAdministratorAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new GetMyAgencyQuery())).Should().ThrowAsync<ForbiddenAccessException>();
    }
}
