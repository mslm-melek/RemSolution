using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Facture.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand
{
    // Issues the client invoice for a renting. Like the contract, generating
    // again produces the NEXT numbered invoice rather than replacing the
    // previous one — an issued invoice is never edited, a correction is a new
    // document (see the Facture entity).
    [Authorize(Policy = Permissions.FactureGenerate)]
    [RequiresFeature(FeatureFlags.Factures)]
    [Auditable("GenerateFacture", "Facture")]
    public record GenerateFactureCommand : IRequest<FactureDto>
    {
        public int RentingId { get; init; }

        /// <summary>See GenerateContractCommand.TemplateId.</summary>
        public int? TemplateId { get; init; }

        /// <summary>See GenerateContractCommand.ManualValues.</summary>
        public Dictionary<string, string>? ManualValues { get; init; }
    }

    public class GenerateFactureCommandHandler : IRequestHandler<GenerateFactureCommand, FactureDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IRentalDocumentService _documents;
        private readonly IStoredFileService _storedFiles;
        private readonly IMapper _mapper;

        public GenerateFactureCommandHandler(
            IApplicationDbContext context,
            IRentalDocumentService documents,
            IStoredFileService storedFiles,
            IMapper mapper)
        {
            _context = context;
            _documents = documents;
            _storedFiles = storedFiles;
            _mapper = mapper;
        }

        public async Task<FactureDto> Handle(GenerateFactureCommand request, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            var facture = await _documents.GenerateFactureAsync(
                new RentalDocumentRequest(request.RentingId, request.TemplateId, request.ManualValues),
                cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                // See GenerateContractCommand: don't leave the rendered bytes
                // behind if the row never lands.
                if (facture.DocumentFile is { } file)
                {
                    await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);
                }

                throw;
            }

            return _mapper.Map<FactureDto>(facture);
        }
    }
}

namespace RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand
{
    public class GenerateFactureCommandValidator : AbstractValidator<GenerateFactureCommand>
    {
        public GenerateFactureCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
        }
    }
}
