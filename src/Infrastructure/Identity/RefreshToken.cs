namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// A persisted, revocable refresh token. The raw token is never stored — only
/// its SHA-256 hash — so a database leak cannot be replayed. Each refresh
/// rotates the token (issues a new one, revokes the old), which enables theft
/// detection: presenting an already-rotated token revokes the whole chain.
///
/// Not an ITenantEntity: it is keyed to an Identity user (which spans the
/// platform), and is never surfaced on IApplicationDbContext — only
/// TokenService touches it.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>Owning user (FK to AspNetUsers), configured in ApplicationDbContext.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 hash (Base64) of the raw token. The raw value is returned to the client once and never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The user's security stamp at issue time. On use it is compared against the
    /// current stamp; a mismatch (user disabled, agency reassigned, password
    /// changed) rejects the token — the revocation mechanism the permission model
    /// relies on.
    /// </summary>
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>Set when the token is rotated on refresh or explicitly revoked (logout / theft).</summary>
    public DateTimeOffset? RevokedUtc { get; set; }

    /// <summary>
    /// Hash of the token that replaced this one on rotation. Records the rotation
    /// chain for forensics; reuse detection itself revokes all of the user's
    /// active tokens rather than walking this link.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }
}
