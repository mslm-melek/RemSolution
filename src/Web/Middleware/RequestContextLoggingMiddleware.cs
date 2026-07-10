using RemSolution.Application.Common.Interfaces;
using Serilog.Context;

namespace RemSolution.Web.Middleware;

/// <summary>
/// Establishes the per-request logging scope. It resolves a correlation id
/// (inbound <c>X-Correlation-ID</c> header, or a fresh GUID), echoes it back on
/// the response, stores it on <see cref="ICorrelationContext"/> for the audit
/// trail, and pushes CorrelationId + UserId + AgencyId onto the Serilog
/// <see cref="LogContext"/>. Every log event emitted downstream — MediatR, EF,
/// framework — then carries the tenant scope, without any call site repeating
/// it. Runs after authentication so the identity claims are populated.
/// </summary>
public class RequestContextLoggingMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public RequestContextLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUser user,
        ITenantProvider tenant,
        ICorrelationContext correlation)
    {
        var correlationId = ResolveCorrelationId(context);
        correlation.CorrelationId = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", user.Id))
        using (LogContext.PushProperty("UserName", user.UserName))
        using (LogContext.PushProperty("AgencyId", tenant.AgencyId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
