using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Queries.GetAgencyFeaturesQuery
{
    // Every known feature plus its EFFECTIVE state for the agency: the active
    // plan's features adjusted by per-agency override rows (see the resolver).
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record GetAgencyFeaturesQuery(int AgencyId) : IRequest<IReadOnlyList<AgencyFeatureDto>>;

    public class GetAgencyFeaturesQueryHandler : IRequestHandler<GetAgencyFeaturesQuery, IReadOnlyList<AgencyFeatureDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public GetAgencyFeaturesQueryHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<IReadOnlyList<AgencyFeatureDto>> Handle(GetAgencyFeaturesQuery request, CancellationToken cancellationToken)
        {
            // Act as the agency so the tenant query filter scopes AgencyFeature rows.
            using var _ = AmbientTenant.Push(request.AgencyId);

            var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
                _context, request.AgencyId, _dateTime.GetUtcNow(), cancellationToken);

            return FeatureFlags.All
                .Select(feature => new AgencyFeatureDto(feature, enabled.Contains(feature)))
                .ToList();
        }
    }
}
