using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Features;

/// <summary>
/// Computes an agency's effective feature set (allow-list): the features its
/// active subscription plan includes, adjusted by per-agency
/// <see cref="AgencyFeature"/> override rows (a row forces a feature on or off;
/// no row inherits the plan). An agency with no active subscription has no
/// features. This is the single place that logic lives — used by the feature
/// enforcement behaviour, GetCurrentUser and the agency-features query.
///
/// The <c>AgencyFeatures</c> read relies on the caller already acting as the
/// agency (tenant claim or AmbientTenant.Push), which every call site does.
/// </summary>
public static class AgencyFeatureResolver
{
    public static async Task<HashSet<string>> GetEnabledFeaturesAsync(
        IApplicationDbContext context, int agencyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Plan baseline: the features of the active subscription's plan.
        var planFeatures = await context.AgencySubscriptions
            .AsNoTracking()
            .Where(AgencySubscription.IsActiveFor(agencyId, now))
            .SelectMany(s => s.Plan!.Features.Select(f => f.Feature))
            .ToListAsync(cancellationToken);

        var enabled = new HashSet<string>(planFeatures);

        // Per-agency overrides layered on top.
        var overrides = await context.AgencyFeatures
            .AsNoTracking()
            .Where(f => f.AgencyId == agencyId)
            .Select(f => new { f.Feature, f.Enabled })
            .ToListAsync(cancellationToken);

        foreach (var o in overrides)
        {
            if (o.Enabled)
            {
                enabled.Add(o.Feature);
            }
            else
            {
                enabled.Remove(o.Feature);
            }
        }

        return enabled;
    }
}
