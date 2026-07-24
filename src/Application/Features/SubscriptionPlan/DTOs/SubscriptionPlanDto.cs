namespace RemSolution.Application.Features.SubscriptionPlan.DTOs
{
    public class SubscriptionPlanDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public int MaxUsers { get; init; }
        public decimal Price { get; init; }
        // Feature keys this plan unlocks (see FeatureFlags).
        public IReadOnlyCollection<string> Features { get; init; } = Array.Empty<string>();

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.SubscriptionPlan, SubscriptionPlanDto>()
                    .Map(d => d.Features, s => s.Features.Select(f => f.Feature).ToList());
            }
        }
    }
}
