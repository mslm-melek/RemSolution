using System.Reflection;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
 
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
    }
}
