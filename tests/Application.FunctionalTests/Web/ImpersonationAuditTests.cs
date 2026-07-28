using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Data;

namespace RemSolution.Application.FunctionalTests.Web;

using static Testing;

/// <summary>
/// The audit trail behind platform-admin impersonation. Now that a workspace
/// allows writes and not only reads, this trail is what bounds the capability —
/// so it has to name the acting user, the agency and the verb, and it has to
/// separate the mutations from the reads a browsing session generates. It is also
/// written before the request runs, so an attempted write that then fails
/// validation is still on record.
/// </summary>
public class ImpersonationAuditTests : BaseTestFixture
{
    [Test]
    public async Task AReadAndAWriteAreRecordedUnderDifferentActions()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        await RecordAsync(agencyId, "GET", "/api/Cars");
        await RecordAsync(agencyId, "POST", "/api/Cars");
        await RecordAsync(agencyId, "DELETE", "/api/Cars/7");

        (await CountAsync<AuditLog>(a => a.Action == ImpersonationAuditor.ImpersonatedReadAction))
            .Should().Be(1);
        // A POST and a DELETE are both mutations — neither may hide among the reads.
        (await CountAsync<AuditLog>(a => a.Action == ImpersonationAuditor.ImpersonatedWriteAction))
            .Should().Be(2);
    }

    [Test]
    public async Task TheRowNamesTheActingUserTheAgencyAndTheRequest()
    {
        var userId = await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        await RecordAsync(agencyId, "PUT", "/api/Clients/42");

        var row = (await AllAsync<AuditLog>())
            .Single(a => a.Action == ImpersonationAuditor.ImpersonatedWriteAction);

        row.UserId.Should().Be(userId);
        row.AgencyId.Should().Be(agencyId);
        // Verb and path both, so the trail says what was done and to what.
        row.After.Should().Contain("PUT").And.Contain("/api/Clients/42");
    }

    private static Task RecordAsync(int agencyId, string method, string path) =>
        UsingScopeAsync(async services =>
        {
            var auditor = services.GetRequiredService<IImpersonationAuditor>();
            await auditor.RecordAsync(agencyId, method, path, CancellationToken.None);
            return true;
        });
}
