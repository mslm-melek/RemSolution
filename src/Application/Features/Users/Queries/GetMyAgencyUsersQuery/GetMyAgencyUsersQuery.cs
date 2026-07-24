using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Users.DTOs;
using RemSolution.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Users.Queries.GetMyAgencyUsersQuery
{
    // Agency administrator lists the users of their OWN agency (tenant from the
    // caller's claim — never from the request).
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record GetMyAgencyUsersQuery : IRequest<IReadOnlyList<AgencyUserDto>>;

    public class GetMyAgencyUsersQueryHandler : IRequestHandler<GetMyAgencyUsersQuery, IReadOnlyList<AgencyUserDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;
        private readonly ITenantProvider _tenant;

        public GetMyAgencyUsersQueryHandler(IApplicationDbContext context, IIdentityService identityService, ITenantProvider tenant)
        {
            _context = context;
            _identityService = identityService;
            _tenant = tenant;
        }

        public async Task<IReadOnlyList<AgencyUserDto>> Handle(GetMyAgencyUsersQuery request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var users = await _identityService.GetAgencyUsersAsync(agencyId, cancellationToken);

            if (users.Count == 0)
            {
                return Array.Empty<AgencyUserDto>();
            }

            var userIds = users.Select(u => u.Id).ToList();

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
                    u.Role == Roles.AgencyAdministrator
                        ? Permissions.All
                        : permissionsByUser.TryGetValue(u.Id, out var perms) ? perms : Array.Empty<string>(),
                    u.IsLockedOut))
                .ToList();
        }
    }
}
