namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Hands the platform's catalog templates (<c>ExpenseTypeTemplate</c>,
/// <c>ExtraServicesTypeTemplate</c>) out to agencies as their own copies.
/// <para>
/// The one place that writes catalog rows for agencies other than the caller's:
/// the platform administrator has no tenant, so the query filters would show
/// them nothing and the write interceptor would stamp nothing. It works in
/// set-based SQL and touches only catalog copies, never tenant data.
/// </para>
/// </summary>
public interface ICatalogTemplateCopier
{
    /// <summary>
    /// Gives every active template to every agency that has no copy of it yet —
    /// or to <paramref name="agencyId"/> alone. Idempotent: an agency that
    /// switched its copy off keeps it switched off.
    /// </summary>
    Task CopyMissingAsync(int? agencyId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the template's names and schedule onto every copy the agency has
    /// not customized. Returns how many copies changed.
    /// </summary>
    Task<int> PropagateExpenseTypeAsync(int templateId, CancellationToken cancellationToken);

    /// <summary>Writes the template's names onto every copy not customized.</summary>
    Task<int> PropagateExtraServicesTypeAsync(int templateId, CancellationToken cancellationToken);
}
