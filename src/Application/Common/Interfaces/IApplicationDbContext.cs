using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Interfaces;

public interface IApplicationDbContext
{
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
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
