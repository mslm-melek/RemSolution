namespace RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery
{
    public class GetClientsWithPaginationQueryValidator : AbstractValidator<GetClientsWithPaginationQuery>
    {
        public GetClientsWithPaginationQueryValidator()
        {
            // Non-positive values would reach SQL Server as a negative
            // OFFSET/FETCH and fail with a 500 instead of a 400.
            RuleFor(q => q.PageNumber)
                .GreaterThanOrEqualTo(1);

            // The upper bound only guards against an unbounded scan; the
            // reservation/renting forms legitimately pull the whole client list
            // in one page to fill their pickers (they ask for 1000).
            RuleFor(q => q.PageSize)
                .InclusiveBetween(1, 1000);
        }
    }
}
