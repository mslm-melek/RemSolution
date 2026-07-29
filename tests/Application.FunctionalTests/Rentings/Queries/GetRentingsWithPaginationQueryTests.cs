using RemSolution.Application.Features.Renting.Queries.GetRentingsWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Rentings.Queries;

using static Testing;

// The dashboard's period counts link into this list, so the window it applies
// has to select exactly the rows those counts measured — see the DateBasis and
// ExcludeCancelled filters.
public class GetRentingsWithPaginationQueryTests : BaseTestFixture
{
    private static readonly DateTime MarchStart = new(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AprilStart = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldFilterByTheDateTheWindowIsAppliedTo()
    {
        await SetUpAgencyAsync();

        // Starts in February and ends in March.
        await SeedRentingAsync("ENDS-IN-MARCH",
            new DateTime(2030, 2, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2030, 3, 3, 0, 0, 0, DateTimeKind.Utc));

        // Starts in March and ends in April.
        await SeedRentingAsync("STARTS-IN-MARCH",
            new DateTime(2030, 3, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2030, 4, 3, 0, 0, 0, DateTimeKind.Utc));

        var starting = await SendAsync(new GetRentingsWithPaginationQuery
        {
            FromDate = MarchStart, ToDate = AprilStart, DateBasis = RentingDateBasis.Starts
        });

        starting.TotalCount.Should().Be(1);
        starting.Items.First().CarMatricule.Should().Be("STARTS-IN-MARCH");

        var ending = await SendAsync(new GetRentingsWithPaginationQuery
        {
            FromDate = MarchStart, ToDate = AprilStart, DateBasis = RentingDateBasis.Ends
        });

        ending.TotalCount.Should().Be(1);
        ending.Items.First().CarMatricule.Should().Be("ENDS-IN-MARCH");

        // Both are running at some point inside March.
        var overlapping = await SendAsync(new GetRentingsWithPaginationQuery
        {
            FromDate = MarchStart, ToDate = AprilStart
        });

        overlapping.TotalCount.Should().Be(2);
    }

    [Test]
    public async Task ShouldLeaveOutCancelledRentingsWhenAsked()
    {
        await SetUpAgencyAsync();

        await SeedRentingAsync("LIVE-1", MarchStart, new DateTime(2030, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        await SeedRentingAsync("CALLED-OFF", MarchStart, new DateTime(2030, 3, 5, 0, 0, 0, DateTimeKind.Utc),
            RentingState.Cancelled);

        (await SendAsync(new GetRentingsWithPaginationQuery())).TotalCount.Should().Be(2);

        var live = await SendAsync(new GetRentingsWithPaginationQuery { ExcludeCancelled = true });

        live.TotalCount.Should().Be(1);
        live.Items.First().CarMatricule.Should().Be("LIVE-1");
    }

    private static async Task SetUpAgencyAsync()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
    }

    // Seeded straight into the context rather than through CreateRentingCommand:
    // the point is the stored dates and state, not the booking rules.
    private static async Task SeedRentingAsync(
        string matricule, DateTime start, DateTime end, RentingState state = RentingState.NotYet)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        await AddAsync(new Renting
        {
            CarId = car.Id, StartDate = start, EndDate = end, RentingState = state
        });
    }
}
