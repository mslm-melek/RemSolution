using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Users.DTOs;
using RemSolution.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Users.Queries.GetAgencyUsersQuery
{
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record GetAgencyUsersQuery(int AgencyId) : IRequest<IReadOnlyList<AgencyUserDto>>;

    public class GetAgencyUsersQueryHandler : IRequestHandler<GetAgencyUsersQuery, IReadOnlyList<AgencyUserDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;

        public GetAgencyUsersQueryHandler(IApplicationDbContext context, IIdentityService identityService)
        {
            _context = context;
            _identityService = identityService;
        }

        public async Task<IReadOnlyList<AgencyUserDto>> Handle(GetAgencyUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _identityService.GetAgencyUsersAsync(request.AgencyId, cancellationToken);

            if (users.Count == 0)
            {
                return Array.Empty<AgencyUserDto>();
            }

            var userIds = users.Select(u => u.Id).ToList();

            // One batched read of the permission rows for all users in the agency.
            var permissionsByUser = (await _context.UserPermissions
                    .Where(p => userIds.Contains(p.UserId))
                    .Select(p => new { p.UserId, p.Permission })
                    .ToListAsync(cancellationToken))
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Permission).ToArray());

            return users
                .Select(u => new AgencyUserDto(
                    u.Id,
                    u.UserName,
                    u.FullName,
                    u.Role,
                    // Administrators hold every permission implicitly (mirrors GetCurrentUser).
                    u.Role == Roles.AgencyAdministrator
                        ? Permissions.All
                        : permissionsByUser.TryGetValue(u.Id, out var perms) ? perms : Array.Empty<string>(),
                    u.IsLockedOut))
                .ToList();
        }
    }
}
