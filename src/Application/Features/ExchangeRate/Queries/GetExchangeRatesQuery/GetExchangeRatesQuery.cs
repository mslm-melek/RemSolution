using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ExchangeRate.DTOs;

namespace RemSolution.Application.Features.ExchangeRate.Queries.GetExchangeRatesQuery
{
    // The pairs as the platform quoted them, for the screen that manages them —
    // one row per couple, carrying its id so it can be re-quoted or withdrawn.
    //
    // NOT what the marketplace reads: that is GetDisplayRatesQuery, which
    // expands each quote into both directions and needs no id. Authorization is
    // the endpoint's (platform admin only); nothing here is secret, since the
    // same figures reach every visitor through the public query.
    public record GetExchangeRatesQuery : IRequest<IList<ExchangeRateDto>>;

    public class GetExchangeRatesQueryHandler
        : IRequestHandler<GetExchangeRatesQuery, IList<ExchangeRateDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExchangeRatesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ExchangeRateDto>> Handle(
            GetExchangeRatesQuery request, CancellationToken cancellationToken)
        {
            return await _context.ExchangeRates
                .AsNoTracking()
                .OrderBy(r => r.FromCurrency)
                .ThenBy(r => r.ToCurrency)
                .Select(r => new ExchangeRateDto
                {
                    Id = r.Id,
                    FromCurrency = r.FromCurrency,
                    ToCurrency = r.ToCurrency,
                    Rate = r.Rate,
                    AsOf = r.AsOf,
                    // Both of them, or the screen re-quotes a pinned pair as
                    // unpinned: the edit form prefills from this row, and the
                    // refresh would take the pair back the next morning.
                    RefreshedAt = r.RefreshedAt,
                    IsPinned = r.IsPinned,
                })
                .ToListAsync(cancellationToken);
        }
    }
}
