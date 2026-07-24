namespace RemSolution.Application.Common.Models;

/// <summary>
/// Identity-store projection of an agency user, read through
/// <see cref="Interfaces.IIdentityService"/>. Role and lockout live in the
/// Identity tables (not on <c>IApplicationDbContext</c>); the query handler
/// merges this with the user's permission rows to build the API DTO.
/// </summary>
public record AgencyUserRecord(
    string Id,
    string UserName,
    string? FullName,
    string Role,
    bool IsLockedOut);
