using RemSolution.Domain.Common;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Agency> Agencies { get; }
    DbSet<AgencySettings> AgencySettings { get; }
    DbSet<AgencyFeature> AgencyFeatures { get; }
    DbSet<AgencySubscription> AgencySubscriptions { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Car> Cars { get; }
    DbSet<CarImage> CarImages { get; }
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
    DbSet<StoredFile> StoredFiles { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<UserPermission> UserPermissions { get; }
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

    /// <summary>
    /// Sets the optimistic-concurrency original value of a tracked entity to the
    /// token the client last read, so the update targets that exact row version
    /// and a stale write raises <c>DbUpdateConcurrencyException</c> instead of
    /// silently overwriting another user's change. No-op when
    /// <paramref name="rowVersion"/> is null (client sent no token).
    /// </summary>
    void SetOriginalRowVersion(IHasRowVersion entity, byte[]? rowVersion);
}
