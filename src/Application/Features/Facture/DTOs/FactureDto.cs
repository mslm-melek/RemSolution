using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Facture.DTOs
{
    public class FactureDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public int RentingId { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }

        /// <summary>The number printed on the document, e.g. "FAC-2026-000042".</summary>
        public string Number { get; init; } = string.Empty;

        public DateTime IssuedAt { get; init; }
        public string Language { get; init; } = string.Empty;

        /// <summary>Totals as invoiced — snapshots, not recomputed values.</summary>
        public MoneyDto? RentalAmount { get; init; }
        public MoneyDto? ExtraServicesAmount { get; init; }
        public MoneyDto? TotalAmount { get; init; }

        /// <summary>See <c>ContractDto.DocumentUrl</c>.</summary>
        public string? DocumentUrl { get; init; }

        public long? DocumentSize { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Facture, FactureDto>()
                      .Map(dest => dest.ClientName,
                           src => src.Client != null ? src.Client.FirstName + " " + src.Client.LastName : null)
                      .Map(dest => dest.DocumentUrl,
                           src => src.DocumentFile != null ? src.DocumentFile.Url : null)
                      .Map(dest => dest.DocumentSize,
                           src => src.DocumentFile != null ? (long?)src.DocumentFile.Size : null);
            }
        }
    }
}
