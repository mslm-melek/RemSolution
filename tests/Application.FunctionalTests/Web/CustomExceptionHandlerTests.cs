using System.Globalization;
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

    // The hire's state machine answers exactly like the hold's: either way, the
    // row on screen is stale.
    [Test]
    public async Task ARefusedRentingTransitionIsTheSameConflict()
    {
        var (status, body) = await HandleAsync(
            new InvalidRentingTransitionException(RentingState.Done, "cancelled"));

        status.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("code").GetString().Should().Be("invalid_transition");
        body.GetProperty("from").GetString().Should().Be(nameof(RentingState.Done));

        // Its own title: a hire titled with the reservation wording reads as a
        // different record to whoever is looking at it.
        var (_, hold) = await HandleAsync(
            new InvalidReservationTransitionException(ReservationStatus.Cancelled, "confirmed"));

        var title = body.GetProperty("title").GetString();
        title.Should().NotBeNullOrWhiteSpace();
        title.Should().NotBe("Error.RentingTransition.Title");
        title.Should().NotBe(hold.GetProperty("title").GetString());
    }

    // A broken domain rule is a bad request, and comes back in the same shape a
    // validator's would — so the SPA needs no second way to read it.
    [Test]
    public async Task ABrokenDomainRuleAnswersLikeAValidationFailure()
    {
        var (status, body) = await HandleAsync(
            new DomainRuleException("EndMileage", "Renting.ReturnMileageBelowPickup",
                "The return mileage cannot be less than the pickup mileage."),
            culture: "en");

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.GetProperty("errors").GetProperty("EndMileage")[0].GetString()
            .Should().Contain("return mileage");
    }

    // The Domain cannot translate, so the rule names its wording and the handler
    // answers in the reader's language, arguments included.
    [Test]
    public async Task ABrokenDomainRuleIsAnsweredInTheRequestLanguage()
    {
        var (_, french) = await HandleAsync(
            new DomainRuleException("Note", "CarUnavailability.NoteTooLong",
                "The note cannot be longer than 500 characters.", 500),
            culture: "fr");

        var (_, arabic) = await HandleAsync(
            new DomainRuleException("Status", "AgencyReport.AlreadyUpheld",
                "This report was already upheld."),
            culture: "ar");

        french.GetProperty("errors").GetProperty("Note")[0].GetString()
            .Should().Contain("500").And.Contain("note");
        arabic.GetProperty("errors").GetProperty("Status")[0].GetString()
            .Should().NotBe("This report was already upheld.")
            .And.NotStartWith("DomainRule.");
    }

    // A key nobody translated must not reach the screen as a key.
    [Test]
    public async Task AnUntranslatedDomainRuleFallsBackToItsEnglish()
    {
        var (_, body) = await HandleAsync(
            new DomainRuleException("X", "No.Such.Key", "Plain English."),
            culture: "fr");

        body.GetProperty("errors").GetProperty("X")[0].GetString().Should().Be("Plain English.");
    }

    [Test]
    public async Task ABookingConflictKeepsItsOwnCode()
    {
        var (status, body) = await HandleAsync(new BookingConflictException(
            carId: 7,
            startDate: new DateTime(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2030, 5, 4, 0, 0, 0, DateTimeKind.Utc),
            kind: BookingConflictKind.Reservation,
            conflictingId: 12));

        status.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("code").GetString().Should().Be("booking_conflict");
        // The agency is told what to cancel, not just that it cannot proceed.
        body.GetProperty("conflictKind").GetString().Should().Be("Reservation");
        body.GetProperty("conflictId").GetInt32().Should().Be(12);
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
    private static async Task<(int Status, JsonElement Body)> HandleAsync(
        Exception exception, string? culture = null)
    {
        return await UsingScopeAsync(async services =>
        {
            var handler = new CustomExceptionHandler(services.GetRequiredService<ILocalizer>());
            var context = NewContext();

            // What UseRequestLocalization would have set for the request.
            var previous = CultureInfo.CurrentUICulture;
            if (culture is not null)
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            }

            try
            {
                var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
                handled.Should().BeTrue($"{exception.GetType().Name} must be mapped explicitly");
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }

            context.Response.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(context.Response.Body);

            return (context.Response.StatusCode, document.RootElement.Clone());
        });
    }

    private static DefaultHttpContext NewContext()
        => new() { Response = { Body = new MemoryStream() } };
}
