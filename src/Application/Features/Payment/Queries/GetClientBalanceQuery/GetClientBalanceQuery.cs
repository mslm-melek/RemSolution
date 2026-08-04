using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Credit.Queries;
using RemSolution.Application.Features.Payment.DTOs;
using RemSolution.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Payment.Queries.GetClientBalanceQuery
{
    // A client's account view (P.3.4): charges owed vs. payments made.
    [Authorize(Policy = Permissions.PaymentRead)]
    [RequiresFeature(FeatureFlags.Payments)]
    public record GetClientBalanceQuery(int ClientId) : IRequest<ClientBalanceDto?>;

    public class GetClientBalanceQueryHandler : IRequestHandler<GetClientBalanceQuery, ClientBalanceDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public GetClientBalanceQueryHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task<ClientBalanceDto?> Handle(GetClientBalanceQuery request, CancellationToken cancellationToken)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);

            if (client is null)
            {
                return null;
            }

            var currency = (await _settings.GetAsync(client.AgencyId, cancellationToken)).CurrencyCode;

            // Charged and paid from the one shared projection (see
            // ClientCreditRows): this screen and the credits list are the same
            // question asked about one client and about all of them, and a client
            // whose balance disagreed with their row on the credits list would be
            // a bug nobody could explain.
            var row = await _context.Clients
                .Where(c => c.Id == request.ClientId)
                .ToCreditRows()
                .FirstAsync(cancellationToken);

            return new ClientBalanceDto
            {
                ClientId = client.Id,
                ClientName = $"{client.FirstName} {client.LastName}".Trim(),
                Currency = currency,
                TotalCharged = new MoneyDto(row.Charged, currency),
                TotalPaid = new MoneyDto(row.Paid, currency),
                Balance = new MoneyDto(row.Charged - row.Paid, currency),
            };
        }
    }
}
