using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Common.Security;

/// <summary>
/// The imperative form of the <c>[Authorize(Policy)]</c> +
/// <c>[RequiresFeature]</c> pair that <c>AuthorizationBehaviour</c> and
/// <c>FeatureEnforcementBehaviour</c> apply to a whole request.
/// <para>
/// A command whose attributes gate its PRIMARY action can still do secondary
/// work that belongs to another module — creating a client inline while creating
/// a renting, or generating a contract as part of the same save. The attributes
/// cannot express "and also, only if", so those paths call this instead. The
/// semantics are deliberately identical to the behaviours, including the
/// consequences: refusal is <see cref="ForbiddenAccessException"/> (403), the
/// agency administrator passes every permission policy by role, and a request
/// with no tenant skips the feature half exactly as the behaviour does.
/// </para>
/// <para>
/// Do NOT reach for this when the check covers the whole request — use the
/// attributes, which keep the gate visible on the request type.
/// </para>
/// </summary>
public static class Entitlements
{
    /// <summary>
    /// Throws <see cref="ForbiddenAccessException"/> unless the current user
    /// holds <paramref name="permission"/> and the current agency has
    /// <paramref name="feature"/> enabled.
    /// </summary>
    public static async Task EnsureAsync(
        IUser user,
        IIdentityService identityService,
        IApplicationDbContext context,
        ITenantProvider tenant,
        TimeProvider dateTime,
        string permission,
        string feature,
        CancellationToken cancellationToken)
    {
        if (user.Id is null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await identityService.AuthorizeAsync(user.Id, permission))
        {
            throw new ForbiddenAccessException();
        }

        // No tenant (platform admin, seeding): there is no agency whose
        // entitlement could apply, mirroring FeatureEnforcementBehaviour.
        if (tenant.AgencyId is not int agencyId)
        {
            return;
        }

        var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
            context, agencyId, dateTime.GetUtcNow(), cancellationToken);

        if (!enabled.Contains(feature))
        {
            throw new ForbiddenAccessException();
        }
    }

    /// <summary>
    /// The feature half on its own. For requests whose gate depends on their
    /// PAYLOAD rather than their type — a document-template command needs the
    /// Contracts feature or the Factures feature depending on the template's kind,
    /// which <c>[RequiresFeature]</c> cannot express because the attribute is fixed
    /// at compile time.
    /// </summary>
    public static async Task EnsureFeatureAsync(
        IApplicationDbContext context,
        ITenantProvider tenant,
        TimeProvider dateTime,
        string feature,
        CancellationToken cancellationToken)
    {
        if (tenant.AgencyId is not int agencyId)
        {
            return;
        }

        var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
            context, agencyId, dateTime.GetUtcNow(), cancellationToken);

        if (!enabled.Contains(feature))
        {
            throw new ForbiddenAccessException();
        }
    }
}
