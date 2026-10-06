using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.ExtraServicesType.DTOs
{
    public class ExtraServicesTypeDto
    {
        public int Id { get; init; }
        // As stored: a translation key for an untouched standard type, the
        // agency's own words otherwise. The SPA translates it (catalogName pipe).
        public string? Name { get; init; }
        // The list price, in the agency's currency; null when it has none.
        public MoneyDto? Amount { get; init; }
        public bool IsActive { get; init; }
        // A copy of a platform template, and whether the agency has since renamed it.
        public bool IsStandard { get; init; }
        public bool IsCustomized { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.ExtraServicesType, ExtraServicesTypeDto>()
                      .Map(dest => dest.IsStandard, src => src.TemplateId != null);
            }
        }
    }
}
