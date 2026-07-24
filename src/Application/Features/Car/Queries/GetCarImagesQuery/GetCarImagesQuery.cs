using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Queries.GetCarImagesQuery
{
    [Authorize(Policy = Permissions.CarRead)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record GetCarImagesQuery(int CarId) : IRequest<IList<CarImageDto>>;

    public class GetCarImagesQueryHandler : IRequestHandler<GetCarImagesQuery, IList<CarImageDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetCarImagesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<CarImageDto>> Handle(GetCarImagesQuery request, CancellationToken cancellationToken)
        {
            return await _context.CarImages
                .AsNoTracking()
                .Where(i => i.CarId == request.CarId)
                .OrderBy(i => i.SortOrder)
                .ProjectToType<CarImageDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
