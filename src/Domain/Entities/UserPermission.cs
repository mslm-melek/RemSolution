namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One permission grant (see Constants.Permissions) for one user. Read at
    /// sign-in by the claims principal factory and carried as claims; the FK
    /// to AspNetUsers is configured in ApplicationDbContext (the Identity user
    /// type lives in Infrastructure). Not a tenant entity: the user itself
    /// carries the agency.
    /// </summary>
    public class UserPermission : BaseAuditableEntity
    {
        public string UserId { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;
    }
}
