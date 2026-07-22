using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Infrastructure.Identity;
using RemSolution.Web.Infrastructure;

namespace RemSolution.Web.Endpoints;

public class Auth : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // All three are anonymous: login and refresh mint credentials, and
        // revoke only needs the refresh token itself (the access token may
        // already be expired at logout).
        app.MapGroup(this)
            .MapPost(Login, "login")
            .MapPost(Refresh, "refresh")
            .MapPost(Revoke, "revoke");
    }

    // Verifies the password (with lockout) without issuing an auth cookie, then
    // returns a JWT access token + refresh token pair.
    public async Task<Results<Ok<AuthTokensResponse>, UnauthorizedHttpResult, BadRequest>> Login(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return TypedResults.BadRequest();

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
            return TypedResults.Unauthorized();

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
            return TypedResults.Unauthorized();

        var tokens = await tokenService.IssueTokensAsync(user.Id, cancellationToken);

        return TypedResults.Ok(AuthTokensResponse.From(tokens));
    }

    // Rotates the refresh token and issues a new pair. Invalid, expired, reused
    // or stamp-invalidated tokens throw UnauthorizedAccessException → 401.
    public async Task<Ok<AuthTokensResponse>> Refresh(
        RefreshRequest request,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        var tokens = await tokenService.RefreshAsync(request.RefreshToken, cancellationToken);

        return TypedResults.Ok(AuthTokensResponse.From(tokens));
    }

    // Logout: revoke a refresh token. Idempotent — always 204.
    public async Task<NoContent> Revoke(
        RefreshRequest request,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        await tokenService.RevokeAsync(request.RefreshToken, cancellationToken);

        return TypedResults.NoContent();
    }
}

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    DateTimeOffset AccessTokenExpiresUtc,
    DateTimeOffset RefreshTokenExpiresUtc)
{
    public static AuthTokensResponse From(Application.Common.Models.AuthTokens tokens) =>
        new(tokens.AccessToken, tokens.RefreshToken, "Bearer", tokens.AccessTokenExpiresUtc, tokens.RefreshTokenExpiresUtc);
}
