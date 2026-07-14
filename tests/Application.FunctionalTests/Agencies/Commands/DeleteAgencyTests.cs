using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand;
using RemSolution.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

public class DeleteAgencyTests : BaseTestFixture
{
    [Test]
    public async Task ShouldBeForbiddenForAgencyUsers()
    {
        await RunAsDefaultUserAsync();
        var agencyId = await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new DeleteAgencyCommand(agencyId))).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldBlockDeletionOfAgencyWithTenantDataAndAuditTheCheck()
    {
        await RunAsDefaultUserAsync();
        var agencyId = await AddTestAgencyAsync();

        // Tenant data owned by the agency (stamped by the interceptor).
        await AddAsync(new Car { Matricule = "DEL-001", FirstCirculationDate = DateTime.UtcNow });

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        await FluentActions.Invoking(() =>
            SendAsync(new DeleteAgencyCommand(agencyId))).Should().ThrowAsync<ValidationException>();

        (await FindAsync<Agency>(agencyId)).Should().NotBeNull();

        // The cross-tenant referential check must leave an audit trail in both
        // registers — and the AuditLog row must survive the aborted command.
        (await CountAsync<CrossTenantAccessLog>()).Should().Be(1);
        (await CountAsync<AuditLog>(l => l.Action == "CrossTenantRead")).Should().Be(1);
    }

    [Test]
    public async Task ShouldDeleteEmptyAgencyAndAuditTheCheck()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        SetCurrentAgency(null);

        await SendAsync(new DeleteAgencyCommand(agencyId));

        (await FindAsync<Agency>(agencyId)).Should().BeNull();
        (await CountAsync<CrossTenantAccessLog>()).Should().Be(1);

        // The trail carries both halves of the operation: the cross-tenant
        // read and the delete it served, correlated in the same request.
        (await CountAsync<AuditLog>(l => l.Action == "CrossTenantRead")).Should().Be(1);
        (await CountAsync<AuditLog>(l => l.Action == "DeleteAgency")).Should().Be(1);
    }

    [Test]
    public async Task CrossTenantReadShouldRefuseRequestsWithoutAuditMarker()
    {
        await RunAsPlatformAdministratorAsync();

        // Calling the service outside an [Auditable] request leaves the audit
        // scope empty: the read must be refused, not silently unrecorded.
        await FluentActions.Invoking(() => UsingScopeAsync(sp =>
                sp.GetRequiredService<ICrossTenantAccess>()
                    .BeginAuditedAccessAsync("no audit marker", CancellationToken.None)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[Auditable]*");
    }
}
