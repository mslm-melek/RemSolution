using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RemSolution.Infrastructure.Identity;

/// <inheritdoc cref="ITokenService"/>
public class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsPrincipalFactory;
    private readonly ApplicationDbContext _context;
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly TimeProvider _timeProvider;
    private readonly JwtOptions _jwt;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory,
        ApplicationDbContext context,
        IOptions<IdentityOptions> identityOptions,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _claimsPrincipalFactory = claimsPrincipalFactory;
        _context = context;
        _identityOptions = identityOptions;
        _timeProvider = timeProvider;
        _jwt = jwtOptions.Value;
    }

    public async Task<AuthTokens> IssueTokensAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("User not found.");

        return await IssueForUserAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);

        var stored = await _context.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        var now = _timeProvider.GetUtcNow();

        // A token that was already rotated (revoked but replaced) is being
        // presented again: either the legitimate client replayed an old value
        // or the token was stolen. Either way, treat the whole family as
        // compromised and revoke every active token for the user.
        if (stored.RevokedUtc is not null)
        {
            await RevokeAllActiveForUserAsync(stored.UserId, now, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token has already been used.");
        }

        if (now >= stored.ExpiresUtc)
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var user = await _userManager.FindByIdAsync(stored.UserId)
            ?? throw new UnauthorizedAccessException("User not found.");

        // The security stamp is the revocation switch: disabling the user,
        // reassigning their agency, or changing their password rotates it,
        // which invalidates every refresh token issued against the old stamp.
        var currentStamp = await _userManager.GetSecurityStampAsync(user);
        if (!string.Equals(stored.SecurityStamp, currentStamp, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Session is no longer valid.");
        }

        // Rotate: mint the replacement first so the old row can record it in
        // ReplacedByTokenHash (kept for forensics on the rotation chain).
        var (rawRefreshToken, refreshTokenHash, refreshExpires) = CreateRefreshTokenValues(now);

        stored.RevokedUtc = now;
        stored.ReplacedByTokenHash = refreshTokenHash;

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            SecurityStamp = currentStamp,
            CreatedUtc = now,
            ExpiresUtc = refreshExpires
        });

        await _context.SaveChangesAsync(cancellationToken);

        var (accessToken, accessExpires) = await CreateAccessTokenAsync(user, now);

        return new AuthTokens(accessToken, rawRefreshToken, accessExpires, refreshExpires);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);

        var stored = await _context.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedUtc is not null)
        {
            return;
        }

        stored.RevokedUtc = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthTokens> IssueForUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var (rawRefreshToken, refreshTokenHash, refreshExpires) = CreateRefreshTokenValues(now);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            SecurityStamp = await _userManager.GetSecurityStampAsync(user),
            CreatedUtc = now,
            ExpiresUtc = refreshExpires
        });

        await _context.SaveChangesAsync(cancellationToken);

        var (accessToken, accessExpires) = await CreateAccessTokenAsync(user, now);

        return new AuthTokens(accessToken, rawRefreshToken, accessExpires, refreshExpires);
    }

    private async Task<(string Token, DateTimeOffset ExpiresUtc)> CreateAccessTokenAsync(ApplicationUser user, DateTimeOffset now)
    {
        // Mint the exact claim set the auth cookie carried — AgencyId, roles and
        // permission claims — so tenant resolution and the permission policies
        // behave identically whether the caller presents a cookie or a JWT.
        var principal = await _claimsPrincipalFactory.CreateAsync(user);

        var securityStampClaimType = _identityOptions.Value.ClaimsIdentity.SecurityStampClaimType;

        var claims = principal.Claims
            // The security stamp is a server-side secret; it does the refresh-time
            // revocation check and has no business inside a stateless access token.
            .Where(c => c.Type != securityStampClaimType)
            .ToList();

        // A unique token id makes each access token individually identifiable in logs.
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));

        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Constructing JwtSecurityToken directly (rather than via a
        // SecurityTokenDescriptor) writes claim types verbatim, so ClaimTypes.*
        // survive the round-trip and match what CurrentUser/CurrentTenant read.
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return (accessToken, expires);
    }

    private (string Raw, string Hash, DateTimeOffset ExpiresUtc) CreateRefreshTokenValues(DateTimeOffset now)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw), now.AddDays(_jwt.RefreshTokenDays));
    }

    private async Task RevokeAllActiveForUserAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.RevokedUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
