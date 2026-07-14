namespace RemSolution.Application.Common.Security;

/// <summary>
/// Marks a request as belonging to a feature module (see
/// Domain.Constants.Features) that an agency can have switched off. Enforced
/// by <c>FeatureEnforcementBehaviour</c>: feature off for the current tenant
/// ⇒ 403 — for every member of the agency, administrator included. Same
/// marker-attribute pattern as <c>[Auditable]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class RequiresFeatureAttribute : Attribute
{
    public RequiresFeatureAttribute(string feature)
    {
        Feature = feature;
    }

    public string Feature { get; }
}
