namespace RemSolution.Application.Common.Models;

/// <summary>
/// The token pair returned by sign-in and refresh. The access token is a signed
/// JWT carrying the caller's claims; the refresh token is an opaque credential
/// the client stores and presents to obtain the next pair.
/// </summary>
public record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresUtc,
    DateTimeOffset RefreshTokenExpiresUtc);
