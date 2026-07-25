using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // A car offered on the public marketplace, from any agency.
    public class MarketplaceCarDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public string? BranchName { get; init; }
        public string? Matricule { get; init; }
        public string? ModelName { get; init; }
        public string? BrandName { get; init; }
        public MoneyDto? DailyRate { get; init; }
        public FuelType? FuelType { get; init; }
        public string? Color { get; init; }
        // Primary gallery image (medium), falling back to the legacy single photo.
        public string? ImageUrl { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Car, MarketplaceCarDto>()
                      .Map(d => d.AgencyName, src => src.Agency != null ? src.Agency.Name : null)
                      .Map(d => d.BranchName, src => src.Branch != null ? src.Branch.Name : null)
                      .Map(d => d.ModelName, src => src.Model != null ? src.Model.Name : null)
                      .Map(d => d.BrandName,
                           src => src.Model != null && src.Model.Brand != null ? src.Model.Brand.Name : null)
                      .Map(d => d.ImageUrl,
                           src => src.Images!.Where(i => i.IsPrimary && i.MediumFile != null)
                                             .Select(i => i.MediumFile!.Url)
                                             .FirstOrDefault()
                                  ?? (src.PhotoFile != null ? src.PhotoFile.Url : null));
            }
        }
    }
}
