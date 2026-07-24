namespace RemSolution.Application.Features.Users.DTOs;

public record AgencyUserDto(
    string Id,
    string UserName,
    string? FullName,
    string Role,
    IReadOnlyCollection<string> Permissions,
    bool IsLockedOut);
