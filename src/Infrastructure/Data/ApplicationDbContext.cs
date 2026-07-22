using System.Reflection;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Common;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ITenantProvider _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<AgencyFeature> AgencyFeatures => Set<AgencyFeature>();
    public DbSet<AgencySubscription> AgencySubscriptions => Set<AgencySubscription>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Country> Countries => Set<Country>();

    // Deliberately not on IApplicationDbContext: audit rows are written by
    // CrossTenantAccess via raw SQL and are not for handlers to query.
    public DbSet<CrossTenantAccessLog> CrossTenantAccessLogs => Set<CrossTenantAccessLog>();

    // Deliberately not on IApplicationDbContext: audit rows are written by the
    // AuditSaveChangesInterceptor, never by handlers.
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<ExtraService> ExtraServices => Set<ExtraService>();
    public DbSet<ExtraServicesType> ExtraServicesTypes => Set<ExtraServicesType>();
    public DbSet<ModelCar> ModelCars => Set<ModelCar>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Renting> Rentings => Set<Renting>();
    public DbSet<RentingHistory> RentingHistories => Set<RentingHistory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        => new TransactionScope(await Database.BeginTransactionAsync(cancellationToken));

    public async Task AcquireTenantWriteLockAsync(CancellationToken cancellationToken)
    {
        if (_tenant.AgencyId is not int agencyId)
        {
            return;
        }

        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The tenant write lock is transaction-owned and must be acquired inside a transaction.");
        }

        // sp_getapplock reports failure (timeout, deadlock victim) through its
        // return value, not an error — surface it as an error explicitly.
        await Database.ExecuteSqlAsync($@"
DECLARE @result int;
EXEC @result = sp_getapplock
    @Resource = {$"agency-writes-{agencyId}"},
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;
IF @result < 0 THROW 51000, 'Failed to acquire the agency write lock.', 1;", cancellationToken);
    }

    private sealed class TransactionScope : ITransactionScope
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;

        public TransactionScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken) => _transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Entity<ApplicationUser>()
            .HasOne<Agency>()
            .WithMany()
            .HasForeignKey(u => u.AgencyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configured here rather than in UserPermissionConfiguration because
        // the Identity user type is not visible from that project layer's
        // Domain references. Grants die with the user.
        builder.Entity<UserPermission>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant isolation: every ITenantEntity is filtered to the current
        // tenant. No tenant (anonymous, platform admin) matches nothing.
        // Bypassing these filters is reserved for the marketplace search
        // feature and the audited CrossTenantAccess path — never for
        // agency-facing handlers (pinned by TenantEnforcementTests).
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
        {
            typeof(ApplicationDbContext)
                .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, new object[] { builder });
        }
    }

    // EF parameterizes the _tenant member access per context instance, so the
    // cached model stays correct across requests with different tenants.
    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity
        => builder.Entity<TEntity>().HasQueryFilter(e => e.AgencyId == _tenant.AgencyId);
}
