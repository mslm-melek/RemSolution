using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Commands.SetPrimaryCarImageCommand
{
    // Makes one gallery image the car's sole primary (the card/hero), clearing
    // the flag on the car's other images so exactly one stays primary. Choosing
    // the primary image is an edit of the car: Car.Update.
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("SetPrimaryCarImage", "Car")]
    public record SetPrimaryCarImageCommand(int CarId, int ImageId) : IRequest;

    public class SetPrimaryCarImageCommandHandler : IRequestHandler<SetPrimaryCarImageCommand>
    {
        private readonly IApplicationDbContext _context;

        public SetPrimaryCarImageCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(SetPrimaryCarImageCommand request, CancellationToken cancellationToken)
        {
            // Tenant-filtered: only this agency's images for this car are loaded.
            var images = await _context.CarImages
                .Where(i => i.CarId == request.CarId)
                .ToListAsync(cancellationToken);

            var target = images.FirstOrDefault(i => i.Id == request.ImageId);
            Guard.Against.NotFound(request.ImageId, target);

            foreach (var image in images)
                image.IsPrimary = image.Id == request.ImageId;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
