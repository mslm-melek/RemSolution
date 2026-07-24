using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Agency.Commands.SetAgencyFeatureCommand
{
    // Platform admin switches a feature module on/off for an agency. Upserts the
    // single AgencyFeature row (agency + feature).
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record SetAgencyFeatureCommand : IRequest
    {
        public int AgencyId { get; init; }
        public string Feature { get; init; } = string.Empty;
        public bool Enabled { get; init; }
    }

    public class SetAgencyFeatureCommandHandler : IRequestHandler<SetAgencyFeatureCommand>
    {
        private readonly IApplicationDbContext _context;

        public SetAgencyFeatureCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(SetAgencyFeatureCommand request, CancellationToken cancellationToken)
        {
            if (!await _context.Agencies.AnyAsync(a => a.Id == request.AgencyId, cancellationToken))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.AgencyId), $"Agency '{request.AgencyId}' was not found."),
                });
            }

            // Act as the agency: scopes the lookup via the tenant filter and lets
            // the write interceptor stamp AgencyId on an inserted row.
            using var _ = AmbientTenant.Push(request.AgencyId);

            var row = await _context.AgencyFeatures
                .FirstOrDefaultAsync(f => f.Feature == request.Feature, cancellationToken);

            if (row is null)
            {
                _context.AgencyFeatures.Add(new AgencyFeature
                {
                    AgencyId = request.AgencyId,
                    Feature = request.Feature,
                    Enabled = request.Enabled,
                });
            }
            else
            {
                row.Enabled = request.Enabled;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
