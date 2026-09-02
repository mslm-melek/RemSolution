using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Queries.GetCarUnavailabilitiesQuery
{
    /// <summary>
    /// Declared time off the road. <paramref name="CarId"/> narrows it to one
    /// car's record (the car screen); left out, it is the fleet's, which is what
    /// answers "what is in the garage this month".
    /// <para>
    /// Past blocks are dropped by default: the list exists to plan around, and a
    /// car's whole servicing history belongs to its expenses. Pass
    /// <paramref name="IncludePast"/> to see them anyway.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.CarRead)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record GetCarUnavailabilitiesQuery(int? CarId = null, bool IncludePast = false)
        : IRequest<IList<CarUnavailabilityDto>>;

    public class GetCarUnavailabilitiesQueryHandler
        : IRequestHandler<GetCarUnavailabilitiesQuery, IList<CarUnavailabilityDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public GetCarUnavailabilitiesQueryHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<IList<CarUnavailabilityDto>> Handle(
            GetCarUnavailabilitiesQuery request, CancellationToken cancellationToken)
        {
            var now = _dateTime.GetUtcNow().UtcDateTime;

            var query = _context.CarUnavailabilities.AsNoTracking();

            if (request.CarId is int carId)
            {
                query = query.Where(u => u.CarId == carId);
            }

            if (!request.IncludePast)
            {
                // End is exclusive, so a block ending today is already over.
                query = query.Where(u => u.EndDate > now);
            }

            return await query
                .OrderBy(u => u.StartDate)
                .ThenBy(u => u.Id)
                .Select(u => new CarUnavailabilityDto
                {
                    Id = u.Id,
                    CarId = u.CarId,
                    CarMatricule = u.Car == null ? null : u.Car.Matricule,
                    StartDate = u.StartDate,
                    EndDate = u.EndDate,
                    Reason = u.Reason,
                    Note = u.Note,
                    IsCurrent = u.StartDate <= now && u.EndDate > now,
                })
                .ToListAsync(cancellationToken);
        }
    }
}
