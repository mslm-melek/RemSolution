namespace RemSolution.Domain.Common;

/// <summary>
/// Agency-scoped entity. Implementing this opts the entity into the tenant
/// pipeline: a global query filter on AgencyId and auto-stamping of AgencyId
/// on insert from the current tenant.
/// </summary>
public interface ITenantEntity
{
    int AgencyId { get; set; }
}
