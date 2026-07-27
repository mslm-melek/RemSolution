namespace RemSolution.Application.Features.Contract.DTOs
{
    public class ContractDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public int RentingId { get; init; }

        /// <summary>The number printed on the document, e.g. "CTR-2026-000042".</summary>
        public string Number { get; init; } = string.Empty;

        public DateTime IssuedAt { get; init; }

        /// <summary>Language the PDF was rendered in ("fr"/"ar"/"en").</summary>
        public string Language { get; init; } = string.Empty;

        /// <summary>
        /// Direct URL of the archived PDF. Convenience only — the SPA downloads
        /// through the API route, which re-checks the permission; static file
        /// URLs are not access-controlled.
        /// </summary>
        public string? DocumentUrl { get; init; }

        public long? DocumentSize { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Contract, ContractDto>()
                      .Map(dest => dest.DocumentUrl,
                           src => src.DocumentFile != null ? src.DocumentFile.Url : null)
                      .Map(dest => dest.DocumentSize,
                           src => src.DocumentFile != null ? (long?)src.DocumentFile.Size : null);
            }
        }
    }
}
