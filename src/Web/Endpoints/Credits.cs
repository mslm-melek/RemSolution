using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Credit.DTOs;
using RemSolution.Application.Features.Credit.Queries.GetClientCreditsQuery;
using RemSolution.Application.Features.Credit.Queries.GetCreditsSummaryQuery;
using RemSolution.Application.Features.Credit.Queries.GetExpenseCreditsQuery;

namespace RemSolution.Web.Endpoints;

// Read-only: both sides of the agency's credit position. Settling a debt happens
// through the module that owns it — a client pays via Payments, an expense via
// the expense settlement endpoint — so nothing is written here.
public class Credits : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetCreditsSummary, "summary", Permissions.CreditRead)
            .MapGet(GetClientCredits, "clients", Permissions.CreditRead)
            .MapGet(GetExpenseCredits, "expenses", Permissions.CreditRead);
    }

    public async Task<Ok<CreditsSummaryDto>> GetCreditsSummary(ISender sender)
    {
        var result = await sender.Send(new GetCreditsSummaryQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<ClientCreditDto>>> GetClientCredits(
        ISender sender, [AsParameters] GetClientCreditsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<ExpenseCreditDto>>> GetExpenseCredits(
        ISender sender, [AsParameters] GetExpenseCreditsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
}
