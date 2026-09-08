using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExchangeRate.Commands.DeleteExchangeRateCommand
{
    // Withdraws a quote. A hard delete, unusually for this codebase: a rate is
    // not a record of anything that happened — no invoice, payment or booking
    // refers to one — so removing it leaves nothing dangling. Prices simply stop
    // being offered in that currency.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    [Auditable("DeleteExchangeRate", "ExchangeRate")]
    public record DeleteExchangeRateCommand(int Id) : IRequest;

    public class DeleteExchangeRateCommandHandler : IRequestHandler<DeleteExchangeRateCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteExchangeRateCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteExchangeRateCommand request, CancellationToken cancellationToken)
        {
            var rate = await _context.ExchangeRates
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, rate);

            _context.ExchangeRates.Remove(rate);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
