using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Client.Validation;

namespace RemSolution.Application.Features.Client.Commands.UpdateClientCommand
{
    public class UpdateClientCommandValidator : ClientPayloadValidator<UpdateClientCommand>
    {
        public UpdateClientCommandValidator(IApplicationDbContext context, TimeProvider dateTime)
            : base(context, dateTime)
        {
            RuleFor(c => c.Id)
                .GreaterThan(0);
        }
    }
}
