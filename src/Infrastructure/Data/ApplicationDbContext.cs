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
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<ExtraService> ExtraServices => Set<ExtraService>();
    public DbSet<ExtraServicesType> ExtraServicesTypes => Set<ExtraServicesType>();
    public DbSet<ModelCar> ModelCars => Set<ModelCar>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Renting> Rentings => Set<Renting>();
    public DbSet<RentingHistory> RentingHistories => Set<RentingHistory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Entity<ApplicationUser>()
            .HasOne<Agency>()
            .WithMany()
            .HasForeignKey(u => u.AgencyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tenant isolation: every ITenantEntity is filtered to the current
        // tenant. No tenant (anonymous, platform admin) matches nothing.
        // Cross-tenant reads via IgnoreQueryFilters() are reserved for the
        // marketplace search feature — never for agency-facing handlers.
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
