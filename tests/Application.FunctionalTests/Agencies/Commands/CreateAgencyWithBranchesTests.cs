using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Application.Features.Agency.Models;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

// An agency is created by a platform administrator, who belongs to no agency.
// Branch is an ITenantEntity, so these tests are really about whether the rows
// end up stamped with the agency that was just created — none of these tests
// calls AddTestAgencyAsync, precisely so there is no ambient tenant to hide a
// missing stamp.
public class CreateAgencyWithBranchesTests : BaseTestFixture
{
    [Test]
    public async Task ShouldCreateBranchesStampedWithTheNewAgency()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        var agencyId = await SendAsync(new CreateAgencyCommand
        {
            Name = "Sud Cars",
            CountryId = country.Id,
            Address = "Head office, Sfax",
            Latitude = 34.7406,
            Longitude = 10.7603,
            Branches = new[]
            {
                new AgencyBranchInput
                {
                    Name = "Sfax downtown",
                    CountryId = country.Id,
                    Address = "Rue de la République, Sfax",
                    Latitude = 34.7398,
                    Longitude = 10.7601
                },
                new AgencyBranchInput
                {
                    Name = "Sfax airport",
                    CountryId = country.Id
                }
            }
        });

        var agency = await FindAsync<Agency>(agencyId);

        agency!.Location.Should().NotBeNull();
        agency.Location!.Y.Should().BeApproximately(34.7406, 1e-9);
        agency.Location.X.Should().BeApproximately(10.7603, 1e-9);
        agency.Location.SRID.Should().Be(4326);

        var branches = (await AllIgnoringFiltersAsync<Branch>()).OrderBy(b => b.Name).ToList();

        branches.Should().HaveCount(2);
        branches.Should().OnlyContain(b => b.AgencyId == agencyId);

        var airport = branches.Single(b => b.Name == "Sfax airport");
        airport.Location.Should().BeNull();
        airport.Address.Should().BeNull();

        var downtown = branches.Single(b => b.Name == "Sfax downtown");
        downtown.Address.Should().Be("Rue de la République, Sfax");
        downtown.Location!.Y.Should().BeApproximately(34.7398, 1e-9);
        downtown.Location.X.Should().BeApproximately(10.7601, 1e-9);
    }

    [Test]
    public async Task ShouldCreateAgencyWithoutBranches()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        var agencyId = await SendAsync(new CreateAgencyCommand
        {
            Name = "No branches yet",
            CountryId = country.Id
        });

        (await FindAsync<Agency>(agencyId)).Should().NotBeNull();
        (await AllIgnoringFiltersAsync<Branch>()).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldAllowAnAgencyWithoutAPin()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        var agencyId = await SendAsync(new CreateAgencyCommand
        {
            Name = "Address only",
            CountryId = country.Id,
            Address = "Somewhere not yet on the map"
        });

        (await FindAsync<Agency>(agencyId))!.Location.Should().BeNull();
    }

    [Test]
    public async Task ShouldRejectAnAgencyWithHalfACoordinatePair()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCommand
        {
            Name = "Half located",
            CountryId = country.Id,
            Latitude = 34.7406
        })).Should().ThrowAsync<ValidationException>();
    }

    // The nested branches go through the same validation as the agency, so a bad
    // one fails the whole request rather than being silently dropped.
    [Test]
    public async Task ShouldRejectTheWholeAgencyWhenABranchIsInvalid()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCommand
        {
            Name = "Has a nameless branch",
            CountryId = country.Id,
            Branches = new[]
            {
                new AgencyBranchInput { Name = string.Empty, CountryId = country.Id }
            }
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<Agency>()).Should().Be(0);
        (await AllIgnoringFiltersAsync<Branch>()).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldBeForbiddenForAnAgencyAdministrator()
    {
        await RunAsAgencyAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCommand
        {
            Name = "Not mine to create",
            CountryId = 1
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }
}
