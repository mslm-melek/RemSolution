using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Common.Audit;

/// <summary>Scoped holder for the request's active <see cref="AuditIntent"/>.</summary>
public sealed class AuditScope : IAuditScope
{
    public AuditIntent? Current { get; set; }
}

/// <summary>Scoped holder for the request's correlation id.</summary>
public sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; set; }
}
