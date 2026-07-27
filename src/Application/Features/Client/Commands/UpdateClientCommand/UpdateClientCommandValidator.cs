using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Client.Validation;

namespace RemSolution.Application.Features.Client.Commands.UpdateClientCommand
{
    public class UpdateClientCommandValidator : ClientPayloadValidator<UpdateClientCommand>
    {
        public UpdateClientCommandValidator(IApplicationDbContext context, TimeProvider dateTime, ILocalizer localizer)
            : base(context, dateTime, localizer)
        {
            RuleFor(c => c.Id)
                .GreaterThan(0);
        }
    }
}
