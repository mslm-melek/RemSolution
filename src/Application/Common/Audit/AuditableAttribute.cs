namespace RemSolution.Application.Common.Audit;

/// <summary>
/// Marks a command as security/business sensitive: the <c>AuditableBehaviour</c>
/// opens an audit scope around it, and the audit interceptor records a
/// before/after <c>AuditLog</c> row for every change to the named entity made
/// while the command runs. Audit stays centralized here — a marker on the
/// command — rather than scattered across handlers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AuditableAttribute : Attribute
{
    /// <param name="action">
    /// The business action recorded on the audit row. Defaults to the command
    /// type name (e.g. "DeleteAgencyCommand") when not supplied.
    /// </param>
    /// <param name="entity">
    /// The entity type name whose changes are captured (e.g. "Agency"). When
    /// null, every changed entity in the save is captured.
    /// </param>
    public AuditableAttribute(string? action = null, string? entity = null)
    {
        Action = action;
        Entity = entity;
    }

    public string? Action { get; }

    public string? Entity { get; }
}
