namespace RemSolution.Domain.Common;

/// <summary>
/// Marks an entity that is archived rather than physically removed: a delete
/// sets <see cref="IsDeleted"/> (plus who/when) and the row stays, preserving
/// history and keeping foreign-key references valid. Applied selectively — only
/// where an archive is wanted (Car, Client), never to financial records
/// (Renting/Payment/Reservation, which are never deleted). The
/// <c>SoftDeleteInterceptor</c> turns a <c>Remove()</c> into the flag update,
/// and the global query filter composes <c>!IsDeleted</c> with the tenant
/// predicate so archived rows disappear from normal reads.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
