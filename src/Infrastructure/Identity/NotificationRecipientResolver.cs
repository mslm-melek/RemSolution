using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// Resolves an alert's audience out of the Identity store. See
/// <see cref="INotificationRecipients"/> for the rule.
/// </summary>
public class NotificationRecipientResolver : INotificationRecipients
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;

    public NotificationRecipientResolver(
        UserManager<ApplicationUser> userManager, IApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ForPermissionAsync(
        int agencyId, string permission, CancellationToken cancellationToken)
    {
        var candidates = await ActiveStaffAsync(agencyId, cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<NotificationRecipient>();
        }

        var candidateIds = candidates.Select(c => c.Id).ToList();

        // Two ways to qualify, matching what the authorization policies accept:
        // the administrator role (implicitly every permission) or an explicit
        // grant. Both are read in bulk — the sweep asks this question once per
        // finding, and per-user round trips would make a fleet's worth of alerts
        // a fleet's worth of queries.
        var granted = await _context.UserPermissions
            .AsNoTracking()
            .Where(p => p.Permission == permission && candidateIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var qualified = new HashSet<string>(granted, StringComparer.Ordinal);
        qualified.UnionWith(await AdministratorIdsAsync(agencyId));

        return candidates
            .Where(c => qualified.Contains(c.Id))
            .Select(c => new NotificationRecipient(c.Id, c.Email, c.FullName, c.PreferredLanguage))
            .ToList();
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ForAdministratorsAsync(
        int agencyId, CancellationToken cancellationToken)
    {
        var candidates = await ActiveStaffAsync(agencyId, cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<NotificationRecipient>();
        }

        // Intersected with the active staff rather than read from the role alone,
        // so the lockout rule applies here too.
        var administrators = await AdministratorIdsAsync(agencyId);

        return candidates
            .Where(c => administrators.Contains(c.Id))
            .Select(c => new NotificationRecipient(c.Id, c.Email, c.FullName, c.PreferredLanguage))
            .ToList();
    }

    // The agency's own people. Not query-filtered — ApplicationUser is an
    // Identity type, not an ITenantEntity — so the agency is filtered here.
    private async Task<List<StaffCandidate>> ActiveStaffAsync(
        int agencyId, CancellationToken cancellationToken) =>
        await _userManager.Users
            .AsNoTracking()
            .Where(u => u.AgencyId == agencyId)
            // A deactivated account is locked out until the far future; excluding
            // it here is why alerts do not pile up on somebody who has left.
            .Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
            .Select(u => new StaffCandidate(u.Id, u.Email, u.FullName, u.PreferredLanguage))
            .ToListAsync(cancellationToken);

    private async Task<HashSet<string>> AdministratorIdsAsync(int agencyId)
    {
        var administrators = await _userManager.GetUsersInRoleAsync(Roles.AgencyAdministrator);

        return administrators
            .Where(a => a.AgencyId == agencyId)
            .Select(a => a.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record StaffCandidate(string Id, string? Email, string? FullName, string? PreferredLanguage);
}
