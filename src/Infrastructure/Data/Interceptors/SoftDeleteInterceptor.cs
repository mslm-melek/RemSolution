using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RemSolution.Infrastructure.Data.Interceptors;

/// <summary>
/// Turns a physical delete of an <see cref="ISoftDeletable"/> entity into an
/// archive: the entry is flipped from Deleted to Modified with
/// <c>IsDeleted = true</c> and who/when stamped, so the row survives (history
/// preserved, FK references stay valid) and the global query filter hides it.
/// Registered first, so the audit and audit-stamp interceptors observe the
/// final Modified state. Only <see cref="ISoftDeletable"/> entities are touched
/// — a hard delete of anything else proceeds normally.
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly IUser _user;
    private readonly TimeProvider _dateTime;

    public SoftDeleteInterceptor(IUser user, TimeProvider dateTime)
    {
        _user = user;
        _dateTime = dateTime;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Archive(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Archive(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Archive(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _dateTime.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDeletable softDeletable)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = now;
            softDeletable.DeletedBy = _user.Id;
        }
    }
}
