using System.ComponentModel.DataAnnotations;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// Configuration for the JWT access token / refresh token pair. Bound from the
/// "Jwt" configuration section (appsettings, environment, or Key Vault).
///
/// The access token is deliberately short-lived: it carries the AgencyId, role
/// and permission claims in-line, exactly as the auth cookie used to. It is the
/// JWT analogue of the old "re-validate the ticket every 10 minutes" strategy —
/// a grant or revocation is live within one <see cref="AccessTokenMinutes"/>
/// window, because the next refresh re-runs the claims-principal factory (which
/// re-reads UserPermissions). The refresh token is the long-lived credential and
/// the revocation anchor (persisted, hashed and tied to the security stamp).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Token issuer (the "iss" claim), validated on every request.</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Intended audience (the "aud" claim), validated on every request.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Symmetric signing key for HMAC-SHA256. Must be at least 32 bytes (256
    /// bits); a shorter key is rejected at startup rather than producing weak
    /// signatures. Supply a strong random value via Key Vault / environment in
    /// any non-development environment.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access token lifetime in minutes. Keep short; this is the revocation window.</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token lifetime in days. Mirrors the old 14-day sliding cookie.</summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 14;
}
