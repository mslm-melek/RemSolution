using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.PlatformDashboard.DTOs
{
    // The app owner's overview of the whole installation: how many agencies are
    // on board, whether they are paying, and how much data they hold — the same
    // three questions the platform-admin console answers one agency at a time.
    // Counts are live rather than denormalised: the agency table is small, and a
    // stale figure on this screen is worse than a slightly slower one.
    public class PlatformDashboardDto
    {
        // When the figures were taken; the subscription states below are all
        // relative to this instant.
        public DateTimeOffset GeneratedAt { get; init; }

        // --- Portfolio ---
        public int TotalAgencies { get; init; }
        // Countries with at least one agency, not the size of the reference table.
        public int TotalCountries { get; init; }
        // Live (non-archived) rows across every agency.
        public int TotalCars { get; init; }
        public int TotalClients { get; init; }
        // Of those clients, the ones who hold a portal login (Client.MarketplaceUserId):
        // either they registered on the marketplace themselves, or their agency
        // recorded an email and an account was provisioned for them.
        public int TotalClientAccounts { get; init; }
        // Where the fleet actually is, which is not where the agencies are
        // registered: countries with at least one live car, and the branches
        // ("places") holding them. Cars kept at an agency with no branch count
        // towards their agency's country but towards no place.
        public int CarCountries { get; init; }
        public int CarPlaces { get; init; }

        // --- Subscriptions ---
        // Status Active AND the period covers GeneratedAt — the same rule that
        // decides whether an agency may write (AgencySubscription.IsActiveFor).
        public int ActiveSubscriptions { get; init; }
        // Still flagged Active but the period no longer covers now: the agency is
        // already blocked from writing while the status says otherwise, so this is
        // the platform admin's list to fix.
        public int LapsedSubscriptions { get; init; }
        public int SuspendedSubscriptions { get; init; }
        public int ExpiredSubscriptions { get; init; }
        // Agencies that have never been assigned a subscription at all.
        public int AgenciesWithoutSubscription { get; init; }
        // Active subscriptions ending inside the requested notice window.
        public int SubscriptionsExpiringSoon { get; init; }
        // Agencies whose live car / client count has reached the plan's ceiling —
        // their next create will be refused with a 409.
        public int AgenciesAtCarQuota { get; init; }
        public int AgenciesAtClientQuota { get; init; }
        // Summed list price of the currently-active subscriptions. Plans are
        // platform-level and priced in a single currency, so this carries none.
        public decimal ActivePlanRevenue { get; init; }

        // --- Breakdowns ---
        // One row per country that has agencies, busiest first.
        public IList<PlatformCountryRowDto> Countries { get; init; } = new List<PlatformCountryRowDto>();
        // Every plan in the catalog, including ones nobody is on.
        public IList<PlatformPlanRowDto> Plans { get; init; } = new List<PlatformPlanRowDto>();
        // Every agency, with its current subscription and data volume.
        public IList<PlatformAgencyRowDto> Agencies { get; init; } = new List<PlatformAgencyRowDto>();
    }

    public class PlatformCountryRowDto
    {
        public int CountryId { get; init; }
        // Null when the agency's country row is missing; the screen shows a dash.
        public string? CountryName { get; init; }
        public int Agencies { get; init; }
        public int ActiveSubscriptions { get; init; }
        public int Cars { get; init; }
        public int Clients { get; init; }
    }

    public class PlatformPlanRowDto
    {
        public int PlanId { get; init; }
        public string PlanName { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        // Subscriptions ever written against the plan, any status.
        public int Subscriptions { get; init; }
        // Agencies currently live on it.
        public int ActiveAgencies { get; init; }
    }

    public class PlatformAgencyRowDto
    {
        public int AgencyId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public string? CountryName { get; init; }
        public string Currency { get; init; } = string.Empty;

        // The agency's current subscription: the one covering now if there is
        // one, otherwise the most recently ending. Null when it has none.
        public int? PlanId { get; init; }
        public string? PlanName { get; init; }
        public SubscriptionStatus? SubscriptionStatus { get; init; }
        public bool SubscriptionIsActive { get; init; }
        public DateTimeOffset? SubscriptionEndDate { get; init; }

        public int Cars { get; init; }
        public int Clients { get; init; }
        // Plan ceilings, 0 when the agency has no subscription.
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
    }
}
