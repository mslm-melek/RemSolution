using RemSolution.Application.Features.Agency.Queries.GetAgenciesQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyByIdQuery;

namespace RemSolution.Application.FunctionalTests.Agencies.Queries;

using static Testing;

// Guards the AgencyDto projection after P.9 moved currency onto the 1:1
// AgencySettings navigation: a Mapster ProjectToType over a dependent nav must
// still translate to SQL (else the agency endpoints 500).
public class GetAgencyTests : BaseTestFixture
{
    [Test]
    public async Task GetByIdSurfacesTheSettingsCurrency()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(); // settings currency = TND

        var agency = await SendAsync(new GetAgencyByIdQuery(agencyId));

        agency.Should().NotBeNull();
        agency!.Currency.Should().Be("TND");
        agency.CancellationWindowHours.Should().Be(24);
        agency.ReservationExpiryHours.Should().Be(48);
    }

    [Test]
    public async Task GetAllProjectsWithoutError()
    {
        await RunAsPlatformAdministratorAsync();
        await AddTestAgencyAsync();

        var agencies = await SendAsync(new GetAgenciesQuery());

        agencies.Should().NotBeNull();
        agencies.Should().OnlyContain(a => a.Currency == "TND");
    }
}
