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

            RuleFor(q => q.PageSize)
                .InclusiveBetween(1, 100);
        }
    }
}
