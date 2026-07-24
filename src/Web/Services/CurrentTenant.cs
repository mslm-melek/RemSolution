using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Services;

public class CurrentTenant : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Ambient override wins first: background workers (no HTTP context, no claim)
    // push the agency they act for. In a request the ambient is unset and the
    // AgencyId claim is used.
    public int? AgencyId =>
        AmbientTenant.CurrentAgencyId
        ?? (int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst(Claims.AgencyId)?.Value, out var agencyId)
            ? agencyId
            : null);
}
