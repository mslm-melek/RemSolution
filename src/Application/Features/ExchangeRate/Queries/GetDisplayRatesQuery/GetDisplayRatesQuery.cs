using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ExchangeRate.DTOs;

namespace RemSolution.Application.Features.ExchangeRate.Queries.GetDisplayRatesQuery
{
    /// <summary>
    /// Every rate the marketplace can apply, in both directions. Anonymous, like
    /// the rest of the shop window.
    /// <para>
    /// The platform quotes a pair once (TND → EUR); this returns that row and its
    /// reciprocal, so the browser only ever multiplies. Chains are deliberately
    /// not built — EUR → TND → MAD would compound two roundings into a figure a
    /// visitor might read as a price, and the platform can quote the pair it
    /// wants offered.
    /// </para>
    /// </summary>
    public record GetDisplayRatesQuery : IRequest<IList<DisplayRateDto>>;

    public class GetDisplayRatesQueryHandler
        : IRequestHandler<GetDisplayRatesQuery, IList<DisplayRateDto>>
    {
        // What a reciprocal is rounded to. Six places, like the quoted rate: this
        // multiplies an amount that is itself rounded to two, so the error stays
        // far below the last figure anybody reads.
        private const int RateDecimals = 6;

        private readonly IApplicationDbContext _context;

        public GetDisplayRatesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<DisplayRateDto>> Handle(
            GetDisplayRatesQuery request, CancellationToken cancellationToken)
        {
            var quoted = await _context.ExchangeRates
                .AsNoTracking()
                .Select(r => new { r.FromCurrency, r.ToCurrency, r.Rate, r.AsOf })
                .ToListAsync(cancellationToken);

            var rates = new List<DisplayRateDto>(quoted.Count * 2);

            foreach (var rate in quoted)
            {
                rates.Add(new DisplayRateDto
                {
                    From = rate.FromCurrency,
                    To = rate.ToCurrency,
                    Rate = rate.Rate,
                    AsOf = rate.AsOf,
                });

                // The rate is > 0 by construction (entity and check constraint),
                // so the reciprocal is always defined.
                rates.Add(new DisplayRateDto
                {
                    From = rate.ToCurrency,
                    To = rate.FromCurrency,
                    Rate = Math.Round(1m / rate.Rate, RateDecimals, MidpointRounding.AwayFromZero),
                    AsOf = rate.AsOf,
                });
            }

            return rates
                .OrderBy(r => r.From)
                .ThenBy(r => r.To)
                .ToList();
        }
    }
}
