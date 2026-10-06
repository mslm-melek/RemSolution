namespace RemSolution.Application.Features.ExtraServicesTypeTemplate.DTOs
{
    public class ExtraServicesTypeTemplateDto
    {
        public int Id { get; init; }
        // The translation key; the SPA shows its translation.
        public string? Name { get; init; }
        public bool IsActive { get; init; }
    }
}
