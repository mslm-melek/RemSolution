using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.ExtraService.DTOs
{
    public class ExtraServiceDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public int? RentingId { get; init; }
        public int? ExtraServicesTypeId { get; init; }
        public string? ExtraServicesTypeName { get; init; }
        public MoneyDto? TotalAmount { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.ExtraService, ExtraServiceDto>()
                      .Map(dest => dest.ExtraServicesTypeName,
                           src => src.ExtraServicesType != null ? src.ExtraServicesType.Name : null);
            }
        }
    }
}
