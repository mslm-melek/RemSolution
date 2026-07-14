using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RemSolution.Application.FunctionalTests.Identity;

using static Testing;

/// <summary>
/// Pins the session lifetime strategy: the auth ticket is a short-lived
/// access token (permissions live in it, so revocation is bounded by the
/// security-stamp validation interval), and the refresh re-reads permission
/// grants through the claims principal factory — no version-stamp machinery.
/// </summary>
public class SessionLifetimeTests : BaseTestFixture
{
    [Test]
    public async Task TicketValidationIntervalMustStayShort()
    {
        var options = await UsingScopeAsync(sp =>
            Task.FromResult(sp.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value));

        // 10–15 minutes is the revocation window for permission changes; a
        // longer interval silently weakens revocation.
        options.ValidationInterval.Should().BeGreaterThan(TimeSpan.Zero);
        options.ValidationInterval.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }

    [Test]
    public async Task ApplicationCookieMustSlideAndOutliveTheValidationInterval()
    {
        var cookie = await UsingScopeAsync(sp =>
            Task.FromResult(sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme)));

        // The cookie itself is the long-lived "refresh token": it proves who
        // you are; what you may do is re-derived at each validation.
        cookie.SlidingExpiration.Should().BeTrue();
        cookie.ExpireTimeSpan.Should().Be(TimeSpan.FromDays(14));
    }

    [Test]
    public async Task PrincipalRefreshMustReReadPermissionGrants()
    {
        var staffId = await RunAsAgencyStaffAsync(Permissions.ClientRead);

        // Grant arrives while the session is live.
        await AddAsync(new UserPermission { UserId = staffId, Permission = Permissions.ClientCreate });

        // Re-mint the principal exactly like the security-stamp validator does
        // on refresh: through the claims principal factory.
        var permissions = await UsingScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var factory = sp.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();

            var user = await userManager.FindByIdAsync(staffId);
            var principal = await factory.CreateAsync(user!);

            return principal.FindAll(Claims.Permission).Select(c => c.Value).ToList();
        });

        permissions.Should().BeEquivalentTo(new[] { Permissions.ClientRead, Permissions.ClientCreate });
    }
}
