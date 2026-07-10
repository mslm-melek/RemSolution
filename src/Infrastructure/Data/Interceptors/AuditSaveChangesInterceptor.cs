using System.Text.Json;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RemSolution.Infrastructure.Data.Interceptors;

/// <summary>
/// Writes the business audit trail. It only acts when the request opened an
/// audit scope (a command marked <c>[Auditable]</c>, carried on
/// <see cref="IAuditScope"/>): it then walks the change tracker and, for every
/// inserted/updated/deleted row of the audited entity, appends an
/// <see cref="AuditLog"/> row capturing who/when/agency/action and the
/// before/after state as JSON. Reading before/after here — from
/// OriginalValues vs CurrentValues — is why auditing lives in an interceptor
/// and not the behaviour: this is the only point where old and new values
/// coexist. Rows are added to the same SaveChanges, so the audit commits
/// atomically with the change it describes.
///
/// Registered last so it observes the values the other interceptors have
/// finalised, and skips the platform log tables so it never audits itself.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditScope _auditScope;
    private readonly IUser _user;
    private readonly ITenantProvider _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _dateTime;

    public AuditSaveChangesInterceptor(
        IAuditScope auditScope,
        IUser user,
        ITenantProvider tenant,
        ICorrelationContext correlation,
        TimeProvider dateTime)
    {
        _auditScope = auditScope;
        _user = user;
        _tenant = tenant;
        _correlation = correlation;
        _dateTime = dateTime;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CaptureAudit(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CaptureAudit(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CaptureAudit(DbContext? context)
    {
        if (context is null || _auditScope.Current is not AuditIntent intent)
        {
            return;
        }

        // Materialise first: adding audit rows below mutates the change tracker.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditLog and not CrossTenantAccessLog)
            .Where(e => intent.Entity is null || e.Metadata.ClrType.Name == intent.Entity)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var occurredOn = _dateTime.GetUtcNow();
        var correlationId = _correlation.CorrelationId;
        var userId = _user.Id;

        var logs = entries.Select(entry => new AuditLog
        {
            UserId = userId,
            UserName = _user.UserName,
            // Prefer the acting tenant; fall back to the row's own AgencyId so a
            // platform-admin action on a specific agency's data (e.g. a
            // subscription change) still records which agency it touched.
            AgencyId = _tenant.AgencyId ?? AgencyIdOf(entry),
            Action = intent.Action,
            Entity = entry.Metadata.ClrType.Name,
            EntityId = KeyOf(entry),
            Before = entry.State == EntityState.Added ? null : Serialize(entry, current: false),
            After = entry.State == EntityState.Deleted ? null : Serialize(entry, current: true),
            CorrelationId = correlationId,
            OccurredOn = occurredOn
        }).ToList();

        context.Set<AuditLog>().AddRange(logs);
    }

    private static int? AgencyIdOf(EntityEntry entry)
    {
        var property = entry.Metadata.FindProperty("AgencyId");
        if (property is null)
        {
            return null;
        }

        var propertyEntry = entry.Property(property.Name);
        var value = entry.State == EntityState.Deleted
            ? propertyEntry.OriginalValue
            : propertyEntry.CurrentValue;

        return value as int?;
    }

    private static string? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }

        var values = key.Properties.Select(p =>
        {
            var property = entry.Property(p.Name);
            var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
            return value?.ToString();
        });

        return string.Join("-", values);
    }

    private static string Serialize(EntityEntry entry, bool current)
    {
        var payload = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            // Navigations are excluded (Properties are scalar only); this keeps
            // the payload flat and avoids serialising object graphs.
            var value = current ? property.CurrentValue : property.OriginalValue;
            payload[property.Metadata.Name] = ToJsonSafe(value);
        }

        return JsonSerializer.Serialize(payload);
    }

    // Reduce values to types System.Text.Json handles cleanly; anything exotic
    // (e.g. NetTopologySuite geometry) is stringified rather than blowing up the
    // save the audit is supposed to record.
    private static object? ToJsonSafe(object? value) => value switch
    {
        null => null,
        string or bool or decimal or Guid or DateTime or DateTimeOffset or DateOnly or TimeOnly => value,
        Enum e => e.ToString(),
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double => value,
        // rowversion / concurrency tokens etc. — base64 beats a useless "System.Byte[]".
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value.ToString()
    };
}
