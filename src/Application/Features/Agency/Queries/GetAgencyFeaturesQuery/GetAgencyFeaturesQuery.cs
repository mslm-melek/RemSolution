using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Agency.Queries.GetAgencyFeaturesQuery
{
    // Every known feature plus its effective state for the agency. A feature with
    // no row is enabled by default (see FeatureFlags); a row switches it off/on.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record GetAgencyFeaturesQuery(int AgencyId) : IRequest<IReadOnlyList<AgencyFeatureDto>>;

    public class GetAgencyFeaturesQueryHandler : IRequestHandler<GetAgencyFeaturesQuery, IReadOnlyList<AgencyFeatureDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencyFeaturesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AgencyFeatureDto>> Handle(GetAgencyFeaturesQuery request, CancellationToken cancellationToken)
        {
            // Act as the agency so the tenant query filter scopes AgencyFeature rows.
            using var _ = AmbientTenant.Push(request.AgencyId);

            var rows = await _context.AgencyFeatures
                .AsNoTracking()
                .ToDictionaryAsync(f => f.Feature, f => f.Enabled, cancellationToken);

            return FeatureFlags.All
                .Select(feature => new AgencyFeatureDto(
                    feature,
                    // No row means enabled.
                    rows.TryGetValue(feature, out var enabled) ? enabled : true))
                .ToList();
        }
    }
}
