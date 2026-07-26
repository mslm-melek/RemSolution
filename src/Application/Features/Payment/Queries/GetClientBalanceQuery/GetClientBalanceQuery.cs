using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Payment.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
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

            // Rentings the client is on the hook for (anything not cancelled).
            var rentingCharges = await _context.Rentings
                .Where(r => r.ClientId == request.ClientId
                            && r.RentingState != RentingState.Cancelled
                            && r.Price != null)
                .SumAsync(r => r.Price!.Amount, cancellationToken);

            // Reservations still representing an obligation (confirmed/paid but not
            // yet converted — converted ones have become rentings above).
            var reservationCharges = await _context.Reservations
                .Where(r => r.ClientId == request.ClientId
                            && (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Paid)
                            && r.Price != null)
                .SumAsync(r => r.Price!.Amount, cancellationToken);

            // Net paid: refunds and reversals are negative entries.
            var paid = await _context.Payments
                .Where(p => p.ClientId == request.ClientId && p.PayementAmount != null)
                .SumAsync(p => p.PayementAmount!.Amount, cancellationToken);

            var charged = rentingCharges + reservationCharges;

            return new ClientBalanceDto
            {
                ClientId = client.Id,
                ClientName = $"{client.FirstName} {client.LastName}".Trim(),
                Currency = currency,
                TotalCharged = new MoneyDto(charged, currency),
                TotalPaid = new MoneyDto(paid, currency),
                Balance = new MoneyDto(charged - paid, currency),
            };
        }
    }
}
