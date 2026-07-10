namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Per-request carrier of the active audit intent. The <c>AuditableBehaviour</c>
/// sets <see cref="Current"/> when a command is marked <c>[Auditable]</c>; the
/// audit interceptor reads it during SaveChanges to decide what to record.
/// Null means "this request is not auditable" — the interceptor is a no-op.
/// Scoped, so it is shared by the MediatR request and the DbContext of the same
/// request without threading state through handlers.
/// </summary>
public interface IAuditScope
{
    AuditIntent? Current { get; set; }
}

/// <summary>What to audit: the business action and, optionally, the single entity type to capture.</summary>
/// <param name="Action">Business action label recorded on the audit row.</param>
/// <param name="Entity">Entity type name to capture; null captures every changed entity.</param>
public sealed record AuditIntent(string Action, string? Entity);
