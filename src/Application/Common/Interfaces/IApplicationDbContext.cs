using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Agency> Agencies { get; }
    DbSet<AgencySubscription> AgencySubscriptions { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Car> Cars { get; }
    DbSet<Client> Clients { get; }
    DbSet<Country> Countries { get; }
    DbSet<ExtraService> ExtraServices { get; }
    DbSet<ExtraServicesType> ExtraServicesTypes { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseType> ExpenseTypes { get; }
    DbSet<ModelCar> ModelCars { get; }
    DbSet<Renting> Rentings { get; }
    DbSet<RentingHistory> RentingHistories { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Serializes writers of the current tenant: takes an exclusive app lock
    /// keyed on the tenant's AgencyId, released when the surrounding
    /// transaction commits or rolls back (must be called inside one). A bare
    /// SELECT COUNT before an insert is a race — two concurrent creates both
    /// pass the check; this lock makes count + insert atomic per agency.
    /// No-op without a tenant (seeding, platform admin): there is no
    /// per-agency quota to protect.
    /// </summary>
    Task AcquireTenantWriteLockAsync(CancellationToken cancellationToken);
}
