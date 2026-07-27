using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Users.DTOs;

namespace RemSolution.Application.Features.Users.Queries.GetMyProfileQuery
{
    // The caller's own profile (endpoint requires authentication).
    public record GetMyProfileQuery : IRequest<MyProfileDto>;

    public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, MyProfileDto>
    {
        private readonly IUser _user;
        private readonly IIdentityService _identityService;

        public GetMyProfileQueryHandler(IUser user, IIdentityService identityService)
        {
            _user = user;
            _identityService = identityService;
        }

        public async Task<MyProfileDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var profile = await _identityService.GetProfileAsync(userId)
                ?? throw new UnauthorizedAccessException();

            return new MyProfileDto
            {
                UserName = profile.UserName,
                FullName = profile.FullName,
                Email = profile.Email,
                PreferredLanguage = profile.PreferredLanguage
            };
        }
    }
}
