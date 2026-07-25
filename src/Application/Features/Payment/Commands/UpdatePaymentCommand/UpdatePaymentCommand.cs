using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Payment.Commands.UpdatePaymentCommand
{
    [Authorize(Policy = Permissions.PaymentUpdate)]
    [RequiresFeature(FeatureFlags.Payments)]
    public record UpdatePaymentCommand : IRequest
    {
        public int Id { get; init; }
        public decimal Amount { get; init; }
        public PaymentMethod Method { get; init; }
        public DateTime? PayementDate { get; init; }
        public string? Notes { get; init; }
    }

    public class UpdatePaymentCommandHandler : IRequestHandler<UpdatePaymentCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public UpdatePaymentCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(UpdatePaymentCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.ReversesPaymentId is not null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "A reversal entry cannot be edited.")
                });
            }

            var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

            entity.PayementAmount = Money.Of(request.Amount, settings.CurrencyCode);
            entity.Method = request.Method;
            if (request.PayementDate.HasValue)
            {
                entity.PayementDate = request.PayementDate;
            }
            entity.Notes = request.Notes;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Payment.Commands.UpdatePaymentCommand
{
    public class UpdatePaymentCommandValidator : AbstractValidator<UpdatePaymentCommand>
    {
        public UpdatePaymentCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Amount).GreaterThan(0);
            RuleFor(v => v.Method).IsInEnum();
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
