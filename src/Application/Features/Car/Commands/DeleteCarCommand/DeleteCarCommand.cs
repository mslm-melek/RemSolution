using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Commands.DeleteCarCommand
{

    [Authorize(Policy = Permissions.CarDelete)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("DeleteCar", "Car")]
    public record DeleteCarCommand(int Id) : IRequest;

    public class DeleteCarCommandHandler : IRequestHandler<DeleteCarCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteCarCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Cars
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Cars.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }

    }
}
