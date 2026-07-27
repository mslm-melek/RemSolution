namespace RemSolution.Application.Features.Users.DTOs
{
    // The current user's editable account details. Login/username equals the
    // email; role and agency are read from the current-user endpoint.
    public class MyProfileDto
    {
        public string UserName { get; init; } = string.Empty;
        public string? FullName { get; init; }
        public string? Email { get; init; }

        // Chosen UI language ("en"/"fr"/"ar"), or null when the user has never
        // picked one and the request falls back to cookie / Accept-Language.
        public string? PreferredLanguage { get; init; }
    }
}
