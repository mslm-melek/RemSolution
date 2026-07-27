namespace RemSolution.Application.Common.Models;

/// <summary>Identity projection of the current user's editable profile.</summary>
public record MyProfileRecord(string UserName, string? FullName, string? Email, string? PreferredLanguage);
