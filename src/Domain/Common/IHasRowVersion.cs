namespace RemSolution.Domain.Common;

/// <summary>
/// Marks an entity as carrying a SQL Server <c>rowversion</c> optimistic-
/// concurrency token. Configured centrally in
/// <c>ApplicationDbContext.OnModelCreating</c> (every implementer's
/// <see cref="RowVersion"/> is <c>IsRowVersion()</c>), so an update whose token
/// no longer matches the row raises <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
/// instead of silently overwriting a change made by another user. Clients read
/// the token with the record and send it back on update; the handler restores
/// it as the row's original value so the write targets the exact version the
/// user saw.
/// </summary>
public interface IHasRowVersion
{
    byte[]? RowVersion { get; set; }
}
