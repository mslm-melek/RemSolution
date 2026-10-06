namespace RemSolution.Application.Features.ExpenseTypeTemplate.DTOs
{
    public class ExpenseTypeTemplateDto
    {
        public int Id { get; init; }
        // The translation key; the SPA shows its translation.
        public string? Name { get; init; }
        public bool IsActive { get; init; }
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
    }
}
