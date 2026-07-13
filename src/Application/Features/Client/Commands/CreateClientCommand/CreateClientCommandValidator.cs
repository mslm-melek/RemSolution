using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Client.Validation;

namespace RemSolution.Application.Features.Client.Commands.CreateClientCommand
{
    public class CreateClientCommandValidator : ClientPayloadValidator<CreateClientCommand>
    {
        public CreateClientCommandValidator(IApplicationDbContext context, TimeProvider dateTime)
            : base(context, dateTime)
        {
        }
    }
}
