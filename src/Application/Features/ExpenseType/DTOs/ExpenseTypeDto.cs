namespace RemSolution.Application.Features.ExpenseType.DTOs
{
    public class ExpenseTypeDto
    {
        public int Id { get; init; }
        // As stored: a translation key for an untouched standard type, the
        // agency's own words otherwise. The SPA translates it (catalogName pipe).
        public string? Name { get; init; }
        public bool IsActive { get; init; }
        // Whether an upcoming due (by kilometre/month threshold) should notify.
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
        // A copy of a platform template, and whether the agency has since changed it.
        public bool IsStandard { get; init; }
        public bool IsCustomized { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.ExpenseType, ExpenseTypeDto>()
                      .Map(dest => dest.IsStandard, src => src.TemplateId != null);
            }
        }
    }
}
