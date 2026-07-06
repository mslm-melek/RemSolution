using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencySubscription.DTOs
{
    public class AgencySubscriptionDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public int PlanId { get; init; }
        public string? PlanName { get; init; }
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public decimal Price { get; init; }
        public DateTimeOffset StartDate { get; init; }
        public DateTimeOffset EndDate { get; init; }
        public SubscriptionStatus Status { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.AgencySubscription, AgencySubscriptionDto>()
                    .Map(d => d.AgencyName, s => s.Agency != null ? s.Agency.Name : null)
                    .Map(d => d.PlanName, s => s.Plan != null ? s.Plan.Name : null)
                    .Map(d => d.MaxCars, s => s.Plan != null ? s.Plan.MaxCars : 0)
                    .Map(d => d.MaxClients, s => s.Plan != null ? s.Plan.MaxClients : 0)
                    .Map(d => d.Price, s => s.Plan != null ? s.Plan.Price : 0);
            }
        }
    }
}
