using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Services;

public class CurrentTenant : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? AgencyId =>
        int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst(Claims.AgencyId)?.Value, out var agencyId)
            ? agencyId
            : null;
}
