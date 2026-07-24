using FluentValidation.Results;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Commands.ReorderCarImagesCommand
{
    // Rewrites the gallery order: OrderedImageIds lists every one of the car's
    // images in the desired order, and each image's SortOrder becomes its index
    // in that list. The set must match the car's images exactly (no missing,
    // extra, or duplicate ids). The primary flag is unaffected. Car.Update.
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("ReorderCarImages", "Car")]
    public record ReorderCarImagesCommand(int CarId, IReadOnlyList<int> OrderedImageIds) : IRequest;

    public class ReorderCarImagesCommandHandler : IRequestHandler<ReorderCarImagesCommand>
    {
        private readonly IApplicationDbContext _context;

        public ReorderCarImagesCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(ReorderCarImagesCommand request, CancellationToken cancellationToken)
        {
            // Tenant-filtered: only this agency's images for this car are loaded.
            var images = await _context.CarImages
                .Where(i => i.CarId == request.CarId)
                .ToListAsync(cancellationToken);

            var requested = request.OrderedImageIds;
            var actual = images.Select(i => i.Id).ToHashSet();

            // The requested order must be a permutation of exactly the car's
            // images — reject a partial, padded, or unknown-id list rather than
            // silently leaving some images unordered.
            if (requested.Count != images.Count || !requested.All(actual.Contains))
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new ValidationFailure(
                        nameof(ReorderCarImagesCommand.OrderedImageIds),
                        "OrderedImageIds must list each of the car's images exactly once.")
                });
            }

            var indexById = requested
                .Select((id, index) => (id, index))
                .ToDictionary(x => x.id, x => x.index);

            foreach (var image in images)
                image.SortOrder = indexById[image.Id];

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
