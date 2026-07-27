using RemSolution.Application.Common.Documents;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.DTOs
{
    /// <summary>One placeholder's binding, as the editor and the prompt see it.</summary>
    public class DocumentTemplateFieldDto
    {
        public int Id { get; init; }

        /// <summary>The name between the braces, without them.</summary>
        public string Placeholder { get; init; } = string.Empty;

        public DocumentFieldBinding Binding { get; init; }
        public string? DataPath { get; init; }
        public string? FixedValue { get; init; }
        public string? Label { get; init; }
        public bool IsRequired { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.DocumentTemplateField, DocumentTemplateFieldDto>();
            }
        }
    }

    /// <summary>
    /// A template in list and detail form. Blocks travel as a typed list rather
    /// than the stored JSON string, so the SPA never has to know the persistence
    /// format.
    /// </summary>
    public class DocumentTemplateDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }

        /// <summary>Optimistic-concurrency token; echoed back on update (see P.8).</summary>
        public byte[]? RowVersion { get; init; }

        public string Name { get; init; } = string.Empty;
        public DocumentTemplateKind Kind { get; init; }
        public string Language { get; init; } = string.Empty;
        public bool IsDefault { get; init; }
        public bool IsActive { get; init; }

        public IList<DocumentBlock> Blocks { get; init; } = new List<DocumentBlock>();
        public IList<DocumentTemplateFieldDto> Fields { get; init; } = new List<DocumentTemplateFieldDto>();

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                // Blocks are stored as JSON; the DTO exposes them structurally.
                // Mapster cannot infer that, hence the explicit projection.
                config.NewConfig<Domain.Entities.DocumentTemplate, DocumentTemplateDto>()
                      .Map(dest => dest.Blocks,
                           src => DocumentTemplateBlocks.Deserialize(src.BlocksJson));
            }
        }
    }
}
