using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Users.DTOs;
using RemSolution.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Users.Queries.GetAgencyUserByIdQuery
{
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record GetAgencyUserByIdQuery(string UserId) : IRequest<AgencyUserDto?>;

    public class GetAgencyUserByIdQueryHandler : IRequestHandler<GetAgencyUserByIdQuery, AgencyUserDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;

        public GetAgencyUserByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
        {
            _context = context;
            _identityService = identityService;
        }

        public async Task<AgencyUserDto?> Handle(GetAgencyUserByIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _identityService.GetAgencyUserAsync(request.UserId, cancellationToken);

            if (user is null)
            {
                return null;
            }

            string[] permissions;

            if (user.Role == Roles.AgencyAdministrator)
            {
                permissions = Permissions.All;
            }
            else
            {
                permissions = await _context.UserPermissions
                    .Where(p => p.UserId == user.Id)
                    .Select(p => p.Permission)
                    .ToArrayAsync(cancellationToken);
            }

            return new AgencyUserDto(user.Id, user.UserName, user.FullName, user.Role, permissions, user.IsLockedOut);
        }
    }
}
