using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace RemSolution.Application.FunctionalTests.Identity;

using static Testing;

public class TokenServiceTests : BaseTestFixture
{
    [Test]
    public async Task IssueTokensShouldReturnPairAndPersistActiveRefreshToken()
    {
        var userId = await RunAsDefaultUserAsync();

        var tokens = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.AccessTokenExpiresUtc.Should().BeAfter(DateTimeOffset.UtcNow);
        tokens.RefreshTokenExpiresUtc.Should().BeAfter(tokens.AccessTokenExpiresUtc);

        (await CountAsync<RefreshToken>(t => t.UserId == userId && t.RevokedUtc == null))
            .Should().Be(1);
    }

    [Test]
    public async Task AccessTokenShouldCarryTheSameClaimsAsTheCookie()
    {
        // A staff user with a grant, scoped to an agency: the JWT must carry the
        // AgencyId, role and permission claims the cookie factory would mint, or
        // tenant isolation and the permission policies would behave differently
        // for token callers.
        var userId = await RunAsAgencyStaffAsync(Permissions.ClientCreate);
        var agencyId = await AddTestAgencyAsync();

        await UsingScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            user!.AgencyId = agencyId;
            await userManager.UpdateAsync(user);
            return true;
        });

        var tokens = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == Roles.AgencyStaff);
        jwt.Claims.Should().Contain(c => c.Type == Claims.AgencyId && c.Value == agencyId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == Claims.Permission && c.Value == Permissions.ClientCreate);

        // The security stamp is a server-side secret and must never leak into the token.
        jwt.Claims.Should().NotContain(c => c.Type.Contains("SecurityStamp"));
    }

    [Test]
    public async Task RefreshShouldRotateTheTokenAndIssueANewPair()
    {
        var userId = await RunAsDefaultUserAsync();

        var initial = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        var refreshed = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(initial.RefreshToken, CancellationToken.None));

        refreshed.RefreshToken.Should().NotBe(initial.RefreshToken);
        refreshed.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Exactly one active token (the replacement); the original is now revoked
        // and points at its successor.
        (await CountAsync<RefreshToken>(t => t.UserId == userId && t.RevokedUtc == null))
            .Should().Be(1);
        (await CountAsync<RefreshToken>(t => t.UserId == userId && t.ReplacedByTokenHash != null))
            .Should().Be(1);

        // The rotated token is now usable.
        await FluentActions.Invoking(() => UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(refreshed.RefreshToken, CancellationToken.None)))
            .Should().NotThrowAsync();
    }

    [Test]
    public async Task ReusingARotatedTokenShouldRevokeTheWholeFamily()
    {
        var userId = await RunAsDefaultUserAsync();

        var initial = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        // First use rotates it.
        await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(initial.RefreshToken, CancellationToken.None));

        // Presenting the already-used token again is treated as theft.
        await FluentActions.Invoking(() => UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(initial.RefreshToken, CancellationToken.None)))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        // Every token in the family is revoked — even the legitimate replacement.
        (await CountAsync<RefreshToken>(t => t.UserId == userId && t.RevokedUtc == null))
            .Should().Be(0);
    }

    [Test]
    public async Task ExpiredRefreshTokenShouldBeRejected()
    {
        var userId = await RunAsDefaultUserAsync();

        var tokens = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        await UsingScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var stored = await context.RefreshTokens.SingleAsync(t => t.UserId == userId);
            stored.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
            return true;
        });

        await FluentActions.Invoking(() => UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(tokens.RefreshToken, CancellationToken.None)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task ChangingTheSecurityStampShouldInvalidateOutstandingRefreshTokens()
    {
        var userId = await RunAsDefaultUserAsync();

        var tokens = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        // Simulates AssignAgencyAsync / account disable / password change — all of
        // which refresh the security stamp to kill live sessions.
        await UsingScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            await userManager.UpdateSecurityStampAsync(user!);
            return true;
        });

        await FluentActions.Invoking(() => UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(tokens.RefreshToken, CancellationToken.None)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task RevokedRefreshTokenShouldBeRejected()
    {
        var userId = await RunAsDefaultUserAsync();

        var tokens = await UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        await UsingScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ITokenService>().RevokeAsync(tokens.RefreshToken, CancellationToken.None);
            return true;
        });

        await FluentActions.Invoking(() => UsingScopeAsync(sp =>
            sp.GetRequiredService<ITokenService>().RefreshAsync(tokens.RefreshToken, CancellationToken.None)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
