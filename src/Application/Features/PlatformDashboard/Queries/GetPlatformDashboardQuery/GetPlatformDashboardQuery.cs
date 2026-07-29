using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.PlatformDashboard.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.PlatformDashboard.Queries.GetPlatformDashboardQuery
{
    // The app owner's overview screen. Agencies, plans and subscriptions are all
    // platform-level tables the platform admin reads directly; the car and client
    // counts are tenant data, so they come through the audited cross-tenant path
    // (which is why the query is [Auditable] — a contract pinned by
    // CrossTenantAuditTests and enforced at runtime by CrossTenantAccess).
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    [Auditable("ViewPlatformDashboard", "Agency")]
    public record GetPlatformDashboardQuery(
        // Notice window for SubscriptionsExpiringSoon: how far ahead a renewal
        // counts as imminent.
        int ExpiringWithinDays = 30
    ) : IRequest<PlatformDashboardDto>;

    public class GetPlatformDashboardQueryHandler
        : IRequestHandler<GetPlatformDashboardQuery, PlatformDashboardDto>
    {
        private const int MaxExpiringWithinDays = 365;

        private readonly IApplicationDbContext _context;
        private readonly ICrossTenantAccess _crossTenant;
        private readonly TimeProvider _dateTime;

        public GetPlatformDashboardQueryHandler(
            IApplicationDbContext context, ICrossTenantAccess crossTenant, TimeProvider dateTime)
        {
            _context = context;
            _crossTenant = crossTenant;
            _dateTime = dateTime;
        }

        public async Task<PlatformDashboardDto> Handle(
            GetPlatformDashboardQuery request, CancellationToken cancellationToken)
        {
            var now = _dateTime.GetUtcNow();
            var noticeWindow = Math.Clamp(request.ExpiringWithinDays, 1, MaxExpiringWithinDays);
            var expiringBefore = now.AddDays(noticeWindow);

            // Agency is not ITenantEntity (an agency is not inside a tenant), so
            // the platform admin reads the table directly.
            var agencies = await _context.Agencies
                .AsNoTracking()
                .Select(a => new
                {
                    a.Id,
                    Name = a.Name ?? string.Empty,
                    a.CountryId,
                    CountryName = a.Country != null ? a.Country.Name : null,
                    Currency = a.Settings != null ? a.Settings.CurrencyCode : string.Empty,
                })
                .ToListAsync(cancellationToken);

            // AgencySubscription is platform-level too. Every row is pulled once
            // and the per-agency/per-plan tallies are taken in memory: the totals
            // below slice the same set five different ways, and one round trip
            // beats five aggregate queries at this table size.
            var subscriptions = await _context.AgencySubscriptions
                .AsNoTracking()
                .Select(s => new
                {
                    s.AgencyId,
                    s.PlanId,
                    PlanName = s.Plan != null ? s.Plan.Name : null,
                    Price = s.Plan != null ? s.Plan.Price : 0m,
                    MaxCars = s.Plan != null ? s.Plan.MaxCars : 0,
                    MaxClients = s.Plan != null ? s.Plan.MaxClients : 0,
                    s.Status,
                    s.StartDate,
                    s.EndDate,
                })
                .ToListAsync(cancellationToken);

            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Select(p => new { p.Id, p.Name, p.Price, p.MaxCars, p.MaxClients })
                .ToListAsync(cancellationToken);

            // "Active" here is the domain's single definition of it
            // (AgencySubscription.IsActiveFor): the status alone is not enough,
            // because a lapsed EndDate already blocks the agency's writes.
            bool IsLive(SubscriptionStatus status, DateTimeOffset start, DateTimeOffset end) =>
                status == SubscriptionStatus.Active && start <= now && now < end;

            // The subscription that represents an agency today: the one covering
            // now if there is one, else the latest-ending — same tie-break as
            // GetAgencyUsageQuery, so the two screens never disagree.
            var currentByAgency = subscriptions
                .GroupBy(s => s.AgencyId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s => IsLive(s.Status, s.StartDate, s.EndDate))
                        .ThenByDescending(s => s.EndDate)
                        .First());

            var tenantData = await CountTenantDataAsync(cancellationToken);
            var carsByAgency = tenantData.Cars;
            var clientsByAgency = tenantData.Clients;

            // Where the cars are, which is not the same question as where the
            // agencies are registered: a car sitting at a branch counts towards
            // that branch's country, one kept at the agency towards the agency's.
            var countryOfAgency = agencies.ToDictionary(a => a.Id, a => a.CountryId);

            var carCountries = tenantData.CarPlacements
                .Select(p => p.BranchCountryId ?? countryOfAgency.GetValueOrDefault(p.AgencyId))
                .Where(countryId => countryId != 0)
                .Distinct()
                .Count();

            var carPlaces = tenantData.CarPlacements
                .Where(p => p.BranchId is not null)
                .Select(p => p.BranchId!.Value)
                .Distinct()
                .Count();

            var agencyRows = agencies
                .Select(a =>
                {
                    currentByAgency.TryGetValue(a.Id, out var sub);

                    return new PlatformAgencyRowDto
                    {
                        AgencyId = a.Id,
                        Name = a.Name,
                        CountryId = a.CountryId,
                        CountryName = a.CountryName,
                        Currency = a.Currency,
                        PlanId = sub?.PlanId,
                        PlanName = sub?.PlanName,
                        SubscriptionStatus = sub?.Status,
                        SubscriptionIsActive =
                            sub is not null && IsLive(sub.Status, sub.StartDate, sub.EndDate),
                        SubscriptionEndDate = sub?.EndDate,
                        Cars = carsByAgency.GetValueOrDefault(a.Id),
                        Clients = clientsByAgency.GetValueOrDefault(a.Id),
                        MaxCars = sub?.MaxCars ?? 0,
                        MaxClients = sub?.MaxClients ?? 0,
                    };
                })
                .OrderBy(r => r.CountryName)
                .ThenBy(r => r.Name)
                .ToList();

            // Only countries that actually have agencies get a row — the
            // reference table lists every country on earth, and a page of zeroes
            // is not a breakdown.
            var countryRows = agencyRows
                .GroupBy(r => new { r.CountryId, r.CountryName })
                .Select(g => new PlatformCountryRowDto
                {
                    CountryId = g.Key.CountryId,
                    CountryName = g.Key.CountryName,
                    Agencies = g.Count(),
                    ActiveSubscriptions = g.Count(r => r.SubscriptionIsActive),
                    Cars = g.Sum(r => r.Cars),
                    Clients = g.Sum(r => r.Clients),
                })
                .OrderByDescending(c => c.Agencies)
                .ThenByDescending(c => c.Cars)
                .ThenBy(c => c.CountryName)
                .ToList();

            // Driven off the catalog, not off the subscriptions, so a plan nobody
            // has bought still shows up (with zeroes) instead of vanishing.
            var planRows = plans
                .Select(p => new PlatformPlanRowDto
                {
                    PlanId = p.Id,
                    PlanName = p.Name,
                    Price = p.Price,
                    MaxCars = p.MaxCars,
                    MaxClients = p.MaxClients,
                    Subscriptions = subscriptions.Count(s => s.PlanId == p.Id),
                    ActiveAgencies = agencyRows.Count(r => r.PlanId == p.Id && r.SubscriptionIsActive),
                })
                .OrderByDescending(p => p.ActiveAgencies)
                .ThenBy(p => p.Price)
                .ThenBy(p => p.PlanName)
                .ToList();

            return new PlatformDashboardDto
            {
                GeneratedAt = now,
                TotalAgencies = agencyRows.Count,
                TotalCountries = countryRows.Count,
                TotalCars = agencyRows.Sum(r => r.Cars),
                TotalClients = agencyRows.Sum(r => r.Clients),
                TotalClientAccounts = tenantData.ClientAccounts,
                CarCountries = carCountries,
                CarPlaces = carPlaces,

                ActiveSubscriptions =
                    subscriptions.Count(s => IsLive(s.Status, s.StartDate, s.EndDate)),
                LapsedSubscriptions = subscriptions.Count(s =>
                    s.Status == SubscriptionStatus.Active && !IsLive(s.Status, s.StartDate, s.EndDate)),
                SuspendedSubscriptions =
                    subscriptions.Count(s => s.Status == SubscriptionStatus.Suspended),
                ExpiredSubscriptions =
                    subscriptions.Count(s => s.Status == SubscriptionStatus.Expired),
                AgenciesWithoutSubscription =
                    agencies.Count(a => !currentByAgency.ContainsKey(a.Id)),
                SubscriptionsExpiringSoon = subscriptions.Count(s =>
                    IsLive(s.Status, s.StartDate, s.EndDate) && s.EndDate < expiringBefore),
                // Quota pressure only means something against a real ceiling, so
                // agencies without a plan are left out rather than counted as full.
                AgenciesAtCarQuota =
                    agencyRows.Count(r => r.MaxCars > 0 && r.Cars >= r.MaxCars),
                AgenciesAtClientQuota =
                    agencyRows.Count(r => r.MaxClients > 0 && r.Clients >= r.MaxClients),
                ActivePlanRevenue = subscriptions
                    .Where(s => IsLive(s.Status, s.StartDate, s.EndDate))
                    .Sum(s => s.Price),

                Countries = countryRows,
                Plans = planRows,
                Agencies = agencyRows,
            };
        }

        // What the cross-tenant pass brings back: the per-agency counts the table
        // needs, plus the two platform-wide figures the landing screen shows.
        private sealed record TenantData(
            Dictionary<int, int> Cars,
            Dictionary<int, int> Clients,
            int ClientAccounts,
            IReadOnlyList<CarPlacement> CarPlacements);

        // One row per distinct place a car is kept — not per car, which is why the
        // fleet's geographic spread costs a handful of rows rather than thousands.
        private sealed record CarPlacement(int AgencyId, int? BranchId, int? BranchCountryId);

        // Live car and client counts per agency. The caller has no tenant, so the
        // global tenant filter would hide every row — the audited cross-tenant
        // path is the sanctioned bypass, and only counts escape it, never rows.
        private async Task<TenantData> CountTenantDataAsync(CancellationToken cancellationToken)
        {
            var scope = await _crossTenant.BeginAuditedAccessAsync(
                "Platform dashboard: fleet, client and portal-account totals", cancellationToken);

            // That queryable drops EVERY global filter, soft delete included, so
            // !IsDeleted is re-applied here: an archived car is not part of a
            // live fleet total, and counting it would also disagree with the
            // per-agency usage screen.
            var cars = await scope.Query<Domain.Entities.Car>()
                .Where(c => !c.IsDeleted)
                .GroupBy(c => c.AgencyId)
                .Select(g => new { AgencyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AgencyId, x => x.Count, cancellationToken);

            var clients = await scope.Query<Domain.Entities.Client>()
                .Where(c => !c.IsDeleted)
                .GroupBy(c => c.AgencyId)
                .Select(g => new { AgencyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AgencyId, x => x.Count, cancellationToken);

            // Clients who can sign in: the link to the portal account, however it
            // came about (self-registered on the marketplace, or provisioned from
            // the email their agency recorded).
            var clientAccounts = await scope.Query<Domain.Entities.Client>()
                .Where(c => !c.IsDeleted && c.MarketplaceUserId != null)
                .CountAsync(cancellationToken);

            // Distinct placements, not cars: the countries and branches the fleet
            // is spread across is a handful of rows however large the fleet is.
            var placements = await scope.Query<Domain.Entities.Car>()
                .Where(c => !c.IsDeleted)
                .Select(c => new
                {
                    c.AgencyId,
                    c.BranchId,
                    BranchCountryId = c.Branch != null ? (int?)c.Branch.CountryId : null,
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            return new TenantData(
                cars,
                clients,
                clientAccounts,
                placements
                    .Select(p => new CarPlacement(p.AgencyId, p.BranchId, p.BranchCountryId))
                    .ToList());
        }
    }
}
