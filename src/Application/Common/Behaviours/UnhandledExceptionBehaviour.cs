using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            if (request is ISensitiveRequest)
            {
                // This path fires on every validation failure too, so PII
                // (identity-document numbers) must never be destructured here.
                _logger.LogError(ex, "RemSolution Request: Unhandled Exception for Request {Name} [request body redacted]", requestName);
            }
            else
            {
                _logger.LogError(ex, "RemSolution Request: Unhandled Exception for Request {Name} {@Request}", requestName, request);
            }

            throw;
        }
    }
}
