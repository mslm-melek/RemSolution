using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    public class CarImageDto
    {
        public int Id { get; init; }
        public int CarId { get; init; }
        public int SortOrder { get; init; }
        public bool IsPrimary { get; init; }
        public ImageProcessingStatus ProcessingStatus { get; init; }
        public string? OriginalUrl { get; init; }
        // Null until the background pipeline has generated them.
        public string? ThumbnailUrl { get; init; }
        public string? MediumUrl { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.CarImage, CarImageDto>()
                      .Map(dest => dest.OriginalUrl, src => src.OriginalFile != null ? src.OriginalFile.Url : null)
                      .Map(dest => dest.ThumbnailUrl, src => src.ThumbnailFile != null ? src.ThumbnailFile.Url : null)
                      .Map(dest => dest.MediumUrl, src => src.MediumFile != null ? src.MediumFile.Url : null);
            }
        }
    }
}
