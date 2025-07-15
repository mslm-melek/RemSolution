
namespace RemSolution.Domain.Entities
{
    public class User : BaseAuditableEntity
    {
        public virtual string? UserName { get; set; }
        public virtual string? Email { get; set; }
        public virtual bool EmailConfirmed { get; set; }
        public virtual string? PasswordHash { get; set; }

        public virtual string? PhoneNumber { get; set; }
        public virtual bool PhoneNumberConfirmed { get; set; }
        public virtual bool TwoFactorEnabled { get; set; }
        public virtual DateTimeOffset? LockoutEnd { get; set; }
        public virtual int AccessFailedCount { get; set; }
    }
}
