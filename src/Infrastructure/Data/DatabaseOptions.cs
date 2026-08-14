namespace RemSolution.Infrastructure.Data;

/// <summary>
/// What the application may do to its own database at startup.
/// <para>
/// Migrations are applied by the deployment (one process, before the new code is
/// live), not by the app: two instances racing on __EFMigrationsHistory can leave
/// a half-migrated schema. Seeding stays at startup — it is idempotent reference
/// data, serialised across instances by an application lock.
/// </para>
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// The connection string the NSwag build-time host runs with (see Web.csproj).
    /// There is no database behind it, so anything needing one is skipped.
    /// </summary>
    public const string BuildTimePlaceholderConnectionString = "NSwagBuildTimePlaceholder";

    /// <summary>
    /// Apply pending migrations at startup. Null (the default) means Development only.
    /// </summary>
    public bool? MigrateOnStartup { get; set; }

    /// <summary>Insert the reference data the app cannot run without; idempotent.</summary>
    public bool SeedOnStartup { get; set; } = true;

    /// <summary>
    /// Refuse to start when the database is behind the code, so a deployment that
    /// skipped its migration step fails at the door.
    /// </summary>
    public bool FailOnPendingMigrations { get; set; } = true;

    /// <summary>
    /// The first platform administrator's login. Created only if missing; its
    /// password is never reset from here.
    /// </summary>
    public string PlatformAdminEmail { get; set; } = "platformadmin@localhost";

    /// <summary>
    /// The password that account is created with. Empty outside Development means
    /// "do not create it" — set it from Key Vault for the first deployment, sign
    /// in, change it, remove it.
    /// </summary>
    public string? PlatformAdminPassword { get; set; }
}
