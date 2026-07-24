using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
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
    public async Task ShouldBlockDeletionWhenOnlyArchivedDataRemains()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var brand = new Brand { Name = "Fiat" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "Panda", BrandId = brand.Id };
        await AddAsync(model);
        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "ARCH-1",
            ModelId = model.Id,
            FirstCirculationDate = DateTime.UtcNow
        });

        // Archive the car — the row (and its Restrict FK to the agency) survives.
        await SendAsync(new DeleteCarCommand(carId));

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        // The cross-tenant referential check sees the archived row (it bypasses
        // both the tenant AND the soft-delete filter), so deletion is still
        // blocked with a friendly error rather than a raw FK violation.
        await FluentActions.Invoking(() =>
            SendAsync(new DeleteAgencyCommand(agencyId))).Should().ThrowAsync<ValidationException>();

        (await FindAsync<Agency>(agencyId)).Should().NotBeNull();
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
