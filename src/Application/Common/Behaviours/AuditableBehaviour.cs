using System.Reflection;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Common.Behaviours;

/// <summary>
/// Turns the <c>[Auditable]</c> marker on a command into an active audit scope
/// for the request. The behaviour is only the trigger: it records the intended
/// action/entity, then the audit interceptor captures the actual before/after
/// entity state when the handler saves. This keeps audit logic in one place
/// instead of scattered across handlers, and keeps before/after truthful — it
/// is read from the change tracker, the only place old and new values coexist.
/// Non-auditable requests pass straight through.
/// </summary>
public class AuditableBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly IAuditScope _auditScope;

    public AuditableBehaviour(IAuditScope auditScope)
    {
        _auditScope = auditScope;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var auditable = request.GetType().GetCustomAttribute<AuditableAttribute>();

        if (auditable is null)
        {
            return await next();
        }

        // Confine the intent to this handler's execution and restore whatever was
        // there before. The scope is per-request, so without this a stale intent
        // would leak to any later save in the same scope (e.g. a save triggered
        // by a domain-event handler), mis-attributing or duplicating audit rows.
        var previous = _auditScope.Current;
        _auditScope.Current = new AuditIntent(
            auditable.Action ?? typeof(TRequest).Name,
            auditable.Entity);

        try
        {
            return await next();
        }
        finally
        {
            _auditScope.Current = previous;
        }
    }
}
