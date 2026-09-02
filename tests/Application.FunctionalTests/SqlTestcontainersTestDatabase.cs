using System.Data.Common;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Respawn;
using Testcontainers.MsSql;

namespace RemSolution.Application.FunctionalTests;

public class SqlTestcontainersTestDatabase : ITestDatabase
{
    private const string DefaultDatabase = "RemSolutionTestDb";
    private readonly MsSqlContainer _container;
    private DbConnection _connection = null!;
    private string _connectionString = null!;
    private Respawner _respawner = null!;

    // Pinned rather than left to the library's default: Testcontainers 4.14
    // dropped the parameterless builder precisely so the image a test runs
    // against is stated in the repository and not silently moved by a package
    // bump. 2022-latest is the edition the deployment targets (see
    // infra/core/database/sqlserver/sqlserver.bicep).
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    public SqlTestcontainersTestDatabase()
    {
        _container = new MsSqlBuilder(Image)
            .WithAutoRemove(true)
            .Build();
    }

    public async Task InitialiseAsync()
    {
        await _container.StartAsync();
        await _container.ExecScriptAsync($"CREATE DATABASE {DefaultDatabase}");

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DefaultDatabase
        };

        _connectionString = builder.ConnectionString;

        _connection = new SqlConnection(_connectionString);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning))
            .Options;

        // Migration-only context; no tenant.
        var context = new ApplicationDbContext(options, Mock.Of<ITenantProvider>());

        await context.Database.MigrateAsync();

        _respawner = await Respawner.CreateAsync(_connectionString, new RespawnerOptions
        {
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public DbConnection GetConnection()
    {
        return _connection;
    }

    public string GetConnectionString()
    {
        return _connectionString;
    }

    public async Task ResetAsync()
    {
        await _respawner.ResetAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}
