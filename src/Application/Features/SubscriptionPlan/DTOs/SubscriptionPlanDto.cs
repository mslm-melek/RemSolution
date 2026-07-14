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
    }
}
