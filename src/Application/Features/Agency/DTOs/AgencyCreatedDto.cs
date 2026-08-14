namespace RemSolution.Application.Features.Agency.DTOs
{
    /// <summary>
    /// What creating an agency produced: the id, plus the administrator login and
    /// whether its password reached the holder by mail.
    /// </summary>
    public class AgencyCreatedDto
    {
        public int Id { get; init; }

        /// <summary>The login created for the agency's administrator.</summary>
        public string? AdminUserName { get; init; }

        /// <summary>
        /// The one-time password, shown once and never stored readable. Null when
        /// the account already existed.
        /// </summary>
        public string? AdminTemporaryPassword { get; init; }

        /// <summary>False means the credentials must be handed over by hand.</summary>
        public bool WelcomeEmailSent { get; init; }
    }
}
