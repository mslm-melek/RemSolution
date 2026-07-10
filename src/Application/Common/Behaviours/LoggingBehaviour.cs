using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace RemSolution.Application.Common.Behaviours;

/// <summary>
/// Logs one structured event per incoming request. It no longer resolves the
/// user itself: UserId, AgencyId and CorrelationId are pushed onto the log
/// context by the web request-context middleware and so ride on every event
/// this logger emits. Enriching here instead of duplicating that plumbing keeps
/// the tenant scope consistent across all logs (MediatR, EF, framework).
/// </summary>
public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest> where TRequest : notnull
{
    private readonly ILogger _logger;

    public LoggingBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("RemSolution Request: {Name} {@Request}", requestName, request);

        return Task.CompletedTask;
    }
}
