using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExchangeRate.Commands.SetExchangeRateCommand
{
    /// <summary>
    /// Quotes a pair, or re-quotes it. One command rather than a create and an
    /// update: the pair is the identity, and an administrator typing "TND → EUR"
    /// twice means "this is the rate now", not "make a second row".
    /// </summary>
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    [Auditable("SetExchangeRate", "ExchangeRate")]
    public record SetExchangeRateCommand : IRequest<int>
    {
        public string FromCurrency { get; init; } = string.Empty;
        public string ToCurrency { get; init; } = string.Empty;
        public decimal Rate { get; init; }

        /// <summary>The day the rate is quoted for; today when the caller says nothing.</summary>
        public DateTime? AsOf { get; init; }

        /// <summary>
        /// Keeps the daily automatic refresh off this pair. False by default:
        /// quoting a pair by hand does not, on its own, mean the administrator
        /// wants to keep maintaining it by hand.
        /// </summary>
        public bool IsPinned { get; init; }
    }

    public class SetExchangeRateCommandHandler : IRequestHandler<SetExchangeRateCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public SetExchangeRateCommandHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(SetExchangeRateCommand request, CancellationToken cancellationToken)
        {
            var from = request.FromCurrency.Trim().ToUpperInvariant();
            var to = request.ToCurrency.Trim().ToUpperInvariant();

            // A quote with no date is quoted for today — the date is shown next to
            // the converted price, so it can never be absent.
            var asOf = (request.AsOf ?? _dateTime.GetUtcNow().UtcDateTime).Date;

            var existing = await _context.ExchangeRates
                .FirstOrDefaultAsync(r => r.FromCurrency == from && r.ToCurrency == to, cancellationToken);

            if (existing is not null)
            {
                // Amend, not Refresh: this figure is now a person's, so it stops
                // being reported as automatically maintained.
                existing.Amend(request.Rate, asOf);
                existing.SetPinned(request.IsPinned);
                await _context.SaveChangesAsync(cancellationToken);

                return existing.Id;
            }

            var rate = Domain.Entities.ExchangeRate.Create(from, to, request.Rate, asOf);
            rate.SetPinned(request.IsPinned);

            _context.ExchangeRates.Add(rate);
            await _context.SaveChangesAsync(cancellationToken);

            return rate.Id;
        }
    }
}

namespace RemSolution.Application.Features.ExchangeRate.Commands.SetExchangeRateCommand
{
    public class SetExchangeRateCommandValidator : AbstractValidator<SetExchangeRateCommand>
    {
        public SetExchangeRateCommandValidator()
        {
            RuleFor(v => v.FromCurrency).NotEmpty().Length(3);
            RuleFor(v => v.ToCurrency).NotEmpty().Length(3);

            RuleFor(v => v.ToCurrency)
                .Must((command, to) =>
                    !string.Equals(command.FromCurrency?.Trim(), to?.Trim(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("A rate converts between two different currencies.");

            RuleFor(v => v.Rate).GreaterThan(0m);
        }
    }
}
