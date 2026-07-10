using System.Text.Json;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Data.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace RemSolution.Application.UnitTests.Common.Behaviours;

/// <summary>
/// Runtime coverage for the audit interceptor over the EF Core in-memory
/// provider: proves it captures before/after and writes rows only when an
/// audit scope is open. (Full SQL Server coverage lives in the Testcontainers
/// functional tests.)
/// </summary>
public class AuditInterceptorTests
{
    private AuditScope _auditScope = null!;
    private CorrelationContext _correlation = null!;
    private Mock<IUser> _user = null!;
    private Mock<ITenantProvider> _tenant = null!;

    [SetUp]
    public void Setup()
    {
        _auditScope = new AuditScope();
        _correlation = new CorrelationContext { CorrelationId = "corr-123" };
        _user = new Mock<IUser>();
        _user.Setup(u => u.Id).Returns("user-1");
        _user.Setup(u => u.UserName).Returns("admin@localhost");
        _tenant = new Mock<ITenantProvider>();
        _tenant.Setup(t => t.AgencyId).Returns((int?)null);
    }

    private AuditTestDbContext NewContext()
    {
        var interceptor = new AuditSaveChangesInterceptor(
            _auditScope, _user.Object, _tenant.Object, _correlation, TimeProvider.System);

        var options = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new AuditTestDbContext(options);
    }

    [Test]
    public async Task WritesNoAuditRowWhenScopeIsClosed()
    {
        await using var db = NewContext();
        db.Widgets.Add(new Widget { Price = 10m, AgencyId = 7 });

        await db.SaveChangesAsync();

        db.AuditLogs.Should().BeEmpty();
    }

    [Test]
    public async Task CapturesBeforeAndAfterForAPriceChange()
    {
        await using var db = NewContext();
        var widget = new Widget { Price = 10m, AgencyId = 7 };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        // Open the audit scope, as AuditableBehaviour would for an [Auditable] command.
        _auditScope.Current = new AuditIntent("UpdateWidgetPrice", nameof(Widget));
        widget.Price = 25m;
        await db.SaveChangesAsync();

        var log = db.AuditLogs.Single();
        log.Action.Should().Be("UpdateWidgetPrice");
        log.Entity.Should().Be(nameof(Widget));
        log.UserId.Should().Be("user-1");
        log.UserName.Should().Be("admin@localhost");
        log.CorrelationId.Should().Be("corr-123");
        // Tenant is null (platform action); AgencyId falls back to the row's own column.
        log.AgencyId.Should().Be(7);

        var before = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.Before!)!;
        var after = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.After!)!;
        before["Price"].GetDecimal().Should().Be(10m);
        after["Price"].GetDecimal().Should().Be(25m);
    }

    [Test]
    public async Task CapturesBeforeOnlyForADelete()
    {
        await using var db = NewContext();
        var widget = new Widget { Price = 10m, AgencyId = 7 };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        _auditScope.Current = new AuditIntent("DeleteWidget", nameof(Widget));
        db.Widgets.Remove(widget);
        await db.SaveChangesAsync();

        var log = db.AuditLogs.Single();
        log.Before.Should().NotBeNull();
        log.After.Should().BeNull();
    }

    [Test]
    public async Task IgnoresEntitiesOutsideTheDeclaredEntity()
    {
        await using var db = NewContext();
        var widget = new Widget { Price = 10m, AgencyId = 7 };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync();

        // Scope targets a different entity name, so this change is not recorded.
        _auditScope.Current = new AuditIntent("SomethingElse", "OtherEntity");
        widget.Price = 99m;
        await db.SaveChangesAsync();

        db.AuditLogs.Should().BeEmpty();
    }

    private class Widget
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public int AgencyId { get; set; }
    }

    private class AuditTestDbContext : DbContext
    {
        public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options) : base(options) { }

        public DbSet<Widget> Widgets => Set<Widget>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    }
}
