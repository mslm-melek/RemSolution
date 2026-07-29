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
        public int? BranchId { get; init; }
        public string? BranchName { get; init; }
        // Where the car is picked up, so a result card can be tied to its pin on
        // the map. Null when the car has no branch or the branch is not geocoded
        // — such a car is listed but cannot be placed.
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public string? Matricule { get; init; }
        public string? ModelName { get; init; }
        public string? BrandName { get; init; }
        public MoneyDto? DailyRate { get; init; }
        public FuelType? FuelType { get; init; }
        public string? Color { get; init; }
        // Primary gallery image (medium), falling back to the legacy single photo.
        public string? ImageUrl { get; init; }
        // The seller's public reputation, carried on the card the way a
        // marketplace shows a host's rating next to the listing. Null until the
        // agency has been reviewed at all — an unrated agency is not a bad one.
        public double? AgencyRating { get; init; }
        public int AgencyReviewCount { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Car, MarketplaceCarDto>()
                      .Map(d => d.AgencyName, src => src.Agency != null ? src.Agency.Name : null)
                      .Map(d => d.BranchName, src => src.Branch != null ? src.Branch.Name : null)
                      // Geography stores (longitude, latitude): X is the longitude,
                      // Y the latitude. EF translates both to SQL Server's
                      // .Long / .Lat accessors.
                      .Map(d => d.Latitude,
                           src => src.Branch != null && src.Branch.Location != null
                                  ? (double?)src.Branch.Location.Y
                                  : null)
                      .Map(d => d.Longitude,
                           src => src.Branch != null && src.Branch.Location != null
                                  ? (double?)src.Branch.Location.X
                                  : null)
                      .Map(d => d.ModelName, src => src.Model != null ? src.Model.Name : null)
                      .Map(d => d.BrandName,
                           src => src.Model != null && src.Model.Brand != null ? src.Model.Brand.Name : null)
                      // Reviews are platform-level (see AgencyReview), so this
                      // navigation carries no tenant filter to bypass — the
                      // average is just a correlated aggregate on the card.
                      .Map(d => d.AgencyRating,
                           src => src.Agency != null
                                  ? src.Agency.Reviews!.Average(r => (double?)r.Rating)
                                  : null)
                      .Map(d => d.AgencyReviewCount,
                           src => src.Agency != null ? src.Agency.Reviews!.Count() : 0)
                      .Map(d => d.ImageUrl,
                           src => src.Images!.Where(i => i.IsPrimary && i.MediumFile != null)
                                             .Select(i => i.MediumFile!.Url)
                                             .FirstOrDefault()
                                  ?? (src.PhotoFile != null ? src.PhotoFile.Url : null));
            }
        }
    }
}
