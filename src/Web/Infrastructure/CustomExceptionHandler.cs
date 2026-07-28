using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
// Aliased, not imported: RemSolution.Domain.Exceptions also declares
// NotFoundException and ForbiddenAccessException, which would collide with the
// Ardalis and Application ones this file already maps.
using InvalidReservationTransitionException =
    RemSolution.Domain.Exceptions.InvalidReservationTransitionException;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Web.Infrastructure;

public class CustomExceptionHandler : IExceptionHandler
{
    private readonly Dictionary<Type, Func<HttpContext, Exception, Task>> _exceptionHandlers;

    // Resolved against the request culture, which UseRequestLocalization has
    // already established by the time an exception reaches here.
    private readonly ILocalizer _localizer;

    public CustomExceptionHandler(ILocalizer localizer)
    {
        _localizer = localizer;

        // Register known exception types and handlers.
        _exceptionHandlers = new()
            {
                { typeof(ValidationException), HandleValidationException },
                { typeof(NotFoundException), HandleNotFoundException },
                { typeof(UnauthorizedAccessException), HandleUnauthorizedAccessException },
                { typeof(ForbiddenAccessException), HandleForbiddenAccessException },
                { typeof(SubscriptionRequiredException), HandleSubscriptionRequiredException },
                { typeof(PlanLimitExceededException), HandlePlanLimitExceededException },
                { typeof(BookingConflictException), HandleBookingConflictException },
                { typeof(InvalidReservationTransitionException), HandleInvalidTransitionException },
                { typeof(DbUpdateConcurrencyException), HandleConcurrencyException },
                { typeof(Exception), HandleUnknownException }
            };
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var exceptionType = exception.GetType();

        if (_exceptionHandlers.ContainsKey(exceptionType))
        {
            await _exceptionHandlers[exceptionType].Invoke(httpContext, exception);
            return true;
        }

        return false;
    }

    private async Task HandleValidationException(HttpContext httpContext, Exception ex)
    {
        var exception = (ValidationException)ex;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(new ValidationProblemDetails(exception.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            // The per-field messages already come back localized; without this
            // the envelope's default title would stay English around them.
            Title = _localizer["Error.Validation.Title"],
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        });
    }

    private async Task HandleNotFoundException(HttpContext httpContext, Exception ex)
    {
        var exception = (NotFoundException)ex;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails()
        {
            Status = StatusCodes.Status404NotFound,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = _localizer["Error.NotFound.Title"],
            Detail = exception.Message
        });
    }

    private async Task HandleUnauthorizedAccessException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = _localizer["Error.Unauthorized.Title"],
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        });
    }

    private async Task HandleForbiddenAccessException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = _localizer["Error.Forbidden.Title"],
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
        });
    }
    private async Task HandleSubscriptionRequiredException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status402PaymentRequired;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status402PaymentRequired,
            Title = _localizer["Error.SubscriptionRequired.Title"],
            Detail = ex.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2"
        });
    }

    private async Task HandlePlanLimitExceededException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = _localizer["Error.PlanLimit.Title"],
            Detail = ex.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        });
    }

    private async Task HandleBookingConflictException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = _localizer["Error.BookingConflict.Title"],
            Detail = ex.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        // 409 is also used for plan limits and concurrency; the client keys on
        // this code to show the "car not available" message specifically.
        problemDetails.Extensions["code"] = "booking_conflict";

        await httpContext.Response.WriteAsJsonAsync(problemDetails);
    }

    // A lifecycle method was called from a state that does not allow it — almost
    // always because someone else moved the reservation on since this user loaded
    // the list. That is a conflict, not a server fault: without this the domain
    // exception would fall through to HandleUnknownException and answer 500.
    private async Task HandleInvalidTransitionException(HttpContext httpContext, Exception ex)
    {
        var exception = (InvalidReservationTransitionException)ex;

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = _localizer["Error.ReservationTransition.Title"],
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        // 409 also carries plan limits, booking conflicts and concurrency, so the
        // client keys on this code to reload the row and say what happened.
        problemDetails.Extensions["code"] = "invalid_transition";
        problemDetails.Extensions["from"] = exception.From.ToString();

        await httpContext.Response.WriteAsJsonAsync(problemDetails);
    }

    private async Task HandleConcurrencyException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = _localizer["Error.Concurrency.Title"],
            Detail = _localizer["Error.Concurrency.Detail"],
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        // Machine-readable discriminator: 409 is also used for plan limits, so
        // the client keys on this code to show the "reloaded by another user"
        // message specifically for concurrency conflicts.
        problemDetails.Extensions["code"] = "concurrency_conflict";

        await httpContext.Response.WriteAsJsonAsync(problemDetails);
    }

    private async Task HandleUnknownException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = _localizer["Error.Unknown.Title"],
           // Detail = ex.Message, 
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        });
    }
}
