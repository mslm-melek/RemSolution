using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Behaviours;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand;
using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using FluentAssertions;
using MediatR;
using NUnit.Framework;

namespace RemSolution.Application.UnitTests.Common.Behaviours;

public class AuditableBehaviourTests
{
    private AuditScope _auditScope = null!;

    [SetUp]
    public void Setup()
    {
        _auditScope = new AuditScope();
    }

    [Test]
    public async Task ShouldOpenAuditScopeDuringAuditableCommand()
    {
        var behaviour = new AuditableBehaviour<DeleteAgencyCommand, Unit>(_auditScope);

        // The intent is only active while the handler runs (when the interceptor
        // observes it); capture it inside the delegate.
        AuditIntent? intentDuringHandler = null;
        await behaviour.Handle(
            new DeleteAgencyCommand(1),
            () => { intentDuringHandler = _auditScope.Current; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        intentDuringHandler.Should().NotBeNull();
        intentDuringHandler!.Action.Should().Be("DeleteAgency");
        intentDuringHandler!.Entity.Should().Be("Agency");
    }

    [Test]
    public async Task ShouldRestoreScopeAfterAuditableCommand()
    {
        var behaviour = new AuditableBehaviour<DeleteAgencyCommand, Unit>(_auditScope);

        await behaviour.Handle(
            new DeleteAgencyCommand(1),
            () => Task.FromResult(Unit.Value),
            CancellationToken.None);

        // Restored so a stale intent cannot leak to a later save in the same scope.
        _auditScope.Current.Should().BeNull();
    }

    [Test]
    public async Task ShouldNotOpenAuditScopeForNonAuditableCommand()
    {
        var behaviour = new AuditableBehaviour<CreateBrandCommand, int>(_auditScope);

        await behaviour.Handle(
            new CreateBrandCommand { Name = "BMW" },
            () => Task.FromResult(42),
            CancellationToken.None);

        _auditScope.Current.Should().BeNull();
    }

    [Test]
    public async Task ShouldPassResponseThrough()
    {
        var behaviour = new AuditableBehaviour<CreateBrandCommand, int>(_auditScope);

        var response = await behaviour.Handle(
            new CreateBrandCommand { Name = "BMW" },
            () => Task.FromResult(42),
            CancellationToken.None);

        response.Should().Be(42);
    }
}
