using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Issues and rotates the JWT access token / refresh token pair. The access
/// token carries the same claims the auth cookie used to (AgencyId, roles,
/// permissions), minted through the claims-principal factory so a refresh picks
/// up live permission changes. Revocation is anchored on the refresh token: it
/// is persisted, tied to the user's security stamp, and rotated on every use.
/// </summary>
public interface ITokenService
{
    /// <summary>Issues a fresh token pair for an already-authenticated user (after a password check).</summary>
    Task<AuthTokens> IssueTokensAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates a refresh token and, on success, rotates it and issues a new
    /// pair. Throws <see cref="UnauthorizedAccessException"/> if the token is
    /// unknown, expired, already used/revoked, or the user's security stamp has
    /// changed since it was issued.
    /// </summary>
    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Revokes a refresh token (logout). No-op if the token is unknown or already revoked.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
