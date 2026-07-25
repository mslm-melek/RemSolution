using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;
using ExtraServiceEntity = RemSolution.Domain.Entities.ExtraService;

namespace RemSolution.Application.Features.ExtraService.Commands.CreateExtraServiceCommand
{
    [Authorize(Policy = Permissions.ExtraServiceCreate)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record CreateExtraServiceCommand : IRequest<int>
    {
        public int RentingId { get; init; }
        public int ExtraServicesTypeId { get; init; }
        // Optional override; defaults to the type's catalog Amount.
        public decimal? Amount { get; init; }
    }

    public class CreateExtraServiceCommandHandler : IRequestHandler<CreateExtraServiceCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public CreateExtraServiceCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task<int> Handle(CreateExtraServiceCommand request, CancellationToken cancellationToken)
        {
            var renting = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.RentingId, cancellationToken);

            Guard.Against.NotFound(request.RentingId, renting);

            var type = await _context.ExtraServicesTypes
                .FirstOrDefaultAsync(t => t.Id == request.ExtraServicesTypeId, cancellationToken);

            Guard.Against.NotFound(request.ExtraServicesTypeId, type);

            var amount = request.Amount ?? type.Amount;
            if (amount is not decimal value)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Amount),
                        "No amount was supplied and the selected type has no default price.")
                });
            }

            var settings = await _settings.GetAsync(renting.AgencyId, cancellationToken);

            var entity = new ExtraServiceEntity
            {
                RentingId = renting.Id,
                ExtraServicesTypeId = type.Id,
                TotalAmount = Money.Of(value, settings.CurrencyCode),
            };

            _context.ExtraServices.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.ExtraService.Commands.CreateExtraServiceCommand
{
    public class CreateExtraServiceCommandValidator : AbstractValidator<CreateExtraServiceCommand>
    {
        public CreateExtraServiceCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.ExtraServicesTypeId).GreaterThan(0);
            RuleFor(v => v.Amount).GreaterThanOrEqualTo(0).When(v => v.Amount.HasValue);
        }
    }
}
