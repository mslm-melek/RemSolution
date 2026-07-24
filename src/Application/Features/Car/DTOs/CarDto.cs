using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    public class CarDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        // Optimistic-concurrency token; echoed back on update (see P.8).
        public byte[]? RowVersion { get; init; }
        public string Matricule { get; init; } = string.Empty;
        public int? ModelId { get; init; }
        public string? ModelName { get; init; }
        public int? BranchId { get; init; }
        public string? BranchName { get; init; }
        public CarStatus Status { get; init; }
        public MoneyDto? DailyRate { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        public string? ImageUrl { get; init; }
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Car, CarDto>()
                      .Map(dest => dest.ModelName, src => src.Model != null ? src.Model.Name : string.Empty)
                      .Map(dest => dest.BranchName, src => src.Branch != null ? src.Branch.Name : null)
                      // The photo now lives on a StoredFile; surface the plain
                      // URL so the API contract is unchanged for readers.
                      .Map(dest => dest.ImageUrl, src => src.PhotoFile != null ? src.PhotoFile.Url : null);
            }
        }
    }
}
