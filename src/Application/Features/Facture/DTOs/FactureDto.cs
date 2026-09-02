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

        /// <summary>Charges established at the return (see RentingFee).</summary>
        public MoneyDto? FeesAmount { get; init; }

        /// <summary>Tax-inclusive total of the lines. See <see cref="TotalDue"/>.</summary>
        public MoneyDto? TotalAmount { get; init; }

        // The tax breakdown as frozen on the invoice. Surfaced so the screen can
        // show what the PDF shows without re-opening it — and so it cannot show
        // a figure recomputed from today's rate.
        public MoneyDto? NetAmount { get; init; }
        public decimal VatRatePercent { get; init; }
        public MoneyDto? VatAmount { get; init; }
        public MoneyDto? FiscalStampAmount { get; init; }

        /// <summary>Lines plus duty stamp — what the client owes.</summary>
        public MoneyDto? TotalDue { get; init; }

        /// <summary>The agency's tax number as printed on this invoice.</summary>
        public string? TaxIdentifier { get; init; }

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
