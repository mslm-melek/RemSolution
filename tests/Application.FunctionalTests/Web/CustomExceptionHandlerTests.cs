using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;
using RemSolution.Web.Infrastructure;

namespace RemSolution.Application.FunctionalTests.Web;

using static Testing;

/// <summary>
/// The API's exception → status mapping. Anything not registered in
/// CustomExceptionHandler falls through to 500, so a domain exception that models
/// a client-side conflict has to be listed there explicitly; these tests pin the
/// status and the machine-readable code the SPA keys on, and — by running the
/// real ILocalizer — that each title actually resolves to a resource instead of
/// echoing its key back.
/// </summary>
public class CustomExceptionHandlerTests : BaseTestFixture
{
    [Test]
    public async Task ARefusedReservationTransitionIsAConflictNotAServerFault()
    {
        var (status, body) = await HandleAsync(
            new InvalidReservationTransitionException(ReservationStatus.Cancelled, "confirmed"));

        // 500 here would mean two staff confirming the same hold get "unexpected
        // error" and the log gets a fault that is really a race between users.
        status.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("code").GetString().Should().Be("invalid_transition");
        // The state it was refused from, so the client can be specific.
        body.GetProperty("from").GetString().Should().Be(nameof(ReservationStatus.Cancelled));
        body.GetProperty("detail").GetString().Should().Contain("Cancelled");

        var title = body.GetProperty("title").GetString();
        title.Should().NotBeNullOrWhiteSpace();
        // A missing resource resolves to the key itself (see ILocalizer).
        title.Should().NotBe("Error.ReservationTransition.Title");
    }

    [Test]
    public async Task ABookingConflictKeepsItsOwnCode()
    {
        var (status, body) = await HandleAsync(new BookingConflictException(
            carId: 7,
            startDate: new DateTime(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2030, 5, 4, 0, 0, 0, DateTimeKind.Utc)));

        status.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("code").GetString().Should().Be("booking_conflict");
    }

    [Test]
    public async Task AnUnregisteredExceptionIsNotHandledHere()
    {
        var handled = await UsingScopeAsync(async services =>
        {
            var handler = new CustomExceptionHandler(services.GetRequiredService<ILocalizer>());
            var context = NewContext();

            return await handler.TryHandleAsync(context, new NotImplementedException(), CancellationToken.None);
        });

        // TryHandleAsync only claims the types it registered; ASP.NET's own
        // pipeline turns the rest into a 500.
        handled.Should().BeFalse();
    }

    // Runs one exception through the real handler and returns what a client would
    // receive.
    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        return await UsingScopeAsync(async services =>
        {
            var handler = new CustomExceptionHandler(services.GetRequiredService<ILocalizer>());
            var context = NewContext();

            var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
            handled.Should().BeTrue($"{exception.GetType().Name} must be mapped explicitly");

            context.Response.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(context.Response.Body);

            return (context.Response.StatusCode, document.RootElement.Clone());
        });
    }

    private static DefaultHttpContext NewContext()
        => new() { Response = { Body = new MemoryStream() } };
}
