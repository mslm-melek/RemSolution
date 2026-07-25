using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using PaymentEntity = RemSolution.Domain.Entities.Payment;

namespace RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand
{
    [Authorize(Policy = Permissions.PaymentCreate)]
    [RequiresFeature(FeatureFlags.Payments)]
    public record CreatePaymentCommand : IRequest<int>
    {
        // A payment settles a renting; the client is taken from the renting.
        public int RentingId { get; init; }
        public decimal Amount { get; init; }
        public PaymentMethod Method { get; init; } = PaymentMethod.Cash;
        public DateTime? PayementDate { get; init; }
        public string? Notes { get; init; }
    }

    public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public CreatePaymentCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
        {
            var renting = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.RentingId, cancellationToken);

            Guard.Against.NotFound(request.RentingId, renting);

            var settings = await _settings.GetAsync(renting.AgencyId, cancellationToken);

            var entity = new PaymentEntity
            {
                RentingId = renting.Id,
                ClientId = renting.ClientId,
                PayementDate = request.PayementDate ?? _dateTime.GetUtcNow().UtcDateTime,
                PayementAmount = Money.Of(request.Amount, settings.CurrencyCode),
                Method = request.Method,
                Notes = request.Notes,
            };

            _context.Payments.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand
{
    public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
    {
        public CreatePaymentCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.Amount).GreaterThan(0);
            RuleFor(v => v.Method).IsInEnum();
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
