namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Per-request correlation id, set once by the web request-context middleware
/// from the inbound <c>X-Correlation-ID</c> header (or a fresh GUID). It is
/// pushed onto every log event via the log context and stamped on audit rows,
/// so a structured log line and the audit trail of the same request can be
/// tied together. Scoped.
/// </summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; set; }
}
