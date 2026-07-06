namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Current tenant, resolved per-request from the authenticated user's AgencyId
/// claim. Null when the caller has no agency (anonymous, platform admin,
/// background jobs) — tenant query filters then match nothing, so tenant data
/// is invisible by default rather than exposed.
/// </summary>
public interface ITenantProvider
{
    int? AgencyId { get; }
}
