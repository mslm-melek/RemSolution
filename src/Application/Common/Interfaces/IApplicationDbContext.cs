using RemSolution.Domain.Common;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Agency> Agencies { get; }
    DbSet<AgencySettings> AgencySettings { get; }
    DbSet<AgencyFeature> AgencyFeatures { get; }
    DbSet<AgencyReview> AgencyReviews { get; }
    DbSet<AgencySubscription> AgencySubscriptions { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Car> Cars { get; }
    DbSet<CarExpenseSchedule> CarExpenseSchedules { get; }
    DbSet<CarImage> CarImages { get; }
    DbSet<CarUnavailability> CarUnavailabilities { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Client> Clients { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<Country> Countries { get; }
    DbSet<DocumentTemplate> DocumentTemplates { get; }
    DbSet<DocumentTemplateField> DocumentTemplateFields { get; }
    DbSet<ExtraService> ExtraServices { get; }
    DbSet<ExtraServicesType> ExtraServicesTypes { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseType> ExpenseTypes { get; }
    DbSet<Facture> Factures { get; }
    DbSet<ModelCar> ModelCars { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Renting> Rentings { get; }
    DbSet<RentingHistory> RentingHistories { get; }
    DbSet<RentingFee> RentingFees { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<ReservationRequirement> ReservationRequirements { get; }
    DbSet<StoredFile> StoredFiles { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<PlanFeature> PlanFeatures { get; }
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
    /// Serializes writers of ONE car, for the availability check + insert race.
    /// Transaction-owned like the tenant lock, and a no-op without a tenant.
    /// <para>
    /// Two rules: every availability path must take it, even one that also holds
    /// the agency lock (paths holding different locks exclude nobody), and when
    /// both are needed the agency lock comes FIRST, or two writers deadlock.
    /// </para>
    /// </summary>
    Task AcquireCarWriteLockAsync(int carId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the optimistic-concurrency original value of a tracked entity to the
    /// token the client last read, so the update targets that exact row version
    /// and a stale write raises <c>DbUpdateConcurrencyException</c> instead of
    /// silently overwriting another user's change. No-op when
    /// <paramref name="rowVersion"/> is null (client sent no token).
    /// </summary>
    void SetOriginalRowVersion(IHasRowVersion entity, byte[]? rowVersion);
}
