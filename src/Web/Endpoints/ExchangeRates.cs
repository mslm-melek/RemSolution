using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.ExchangeRate.Commands.DeleteExchangeRateCommand;
using RemSolution.Application.Features.ExchangeRate.Commands.SetExchangeRateCommand;
using RemSolution.Application.Features.ExchangeRate.DTOs;
using RemSolution.Application.Features.ExchangeRate.Queries.GetExchangeRatesQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

// The platform's display rates. Only the app owner quotes them; the marketplace
// reads the same list anonymously through its own group, so a visitor never
// needs an account to see a price in their own currency.
public class ExchangeRates : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetExchangeRates)
            .MapPut(SetExchangeRate, "")
            .MapDelete(DeleteExchangeRate, "{id}");
    }

    public async Task<Ok<IList<ExchangeRateDto>>> GetExchangeRates(ISender sender)
    {
        var result = await sender.Send(new GetExchangeRatesQuery());
        return TypedResults.Ok(result);
    }

    // PUT, not POST: the pair is the identity and re-quoting it is the same call
    // as quoting it for the first time (see SetExchangeRateCommand).
    public async Task<Ok<int>> SetExchangeRate(ISender sender, SetExchangeRateCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Ok(id);
    }

    public async Task<NoContent> DeleteExchangeRate(ISender sender, int id)
    {
        await sender.Send(new DeleteExchangeRateCommand(id));
        return TypedResults.NoContent();
    }
}
