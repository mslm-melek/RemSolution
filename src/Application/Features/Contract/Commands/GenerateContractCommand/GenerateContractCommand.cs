using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Contract.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Contract.Commands.GenerateContractCommand
{
    // Issues the rental agreement for a renting: numbers it, renders the PDF and
    // archives it (see IRentalDocumentService).
    //
    // Idempotency is deliberately NOT enforced: an agent regenerates after the
    // booking is corrected or extra services are added, and each generation is a
    // new numbered document rather than an overwrite, so the copy the client
    // already signed stays retrievable. The SPA shows the full list.
    [Authorize(Policy = Permissions.ContractGenerate)]
    [RequiresFeature(FeatureFlags.Contracts)]
    [Auditable("GenerateContract", "Contract")]
    public record GenerateContractCommand : IRequest<ContractDto>
    {
        public int RentingId { get; init; }

        /// <summary>
        /// The agency template to use. Null falls back to the agency's default and
        /// then to the platform's shipped example, so this works for an agency that
        /// has never opened the template screen.
        /// </summary>
        public int? TemplateId { get; init; }

        /// <summary>
        /// Values for the template's ask-each-time placeholders, keyed by
        /// placeholder name (see GetDocumentPromptQuery for what to ask).
        /// </summary>
        public Dictionary<string, string>? ManualValues { get; init; }
    }

    public class GenerateContractCommandHandler : IRequestHandler<GenerateContractCommand, ContractDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IRentalDocumentService _documents;
        private readonly IStoredFileService _storedFiles;
        private readonly IMapper _mapper;

        public GenerateContractCommandHandler(
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

        public async Task<ContractDto> Handle(GenerateContractCommand request, CancellationToken cancellationToken)
        {
            Domain.Entities.Contract contract;

            // Numbering is MAX(SequenceNumber) + 1, so the read and the insert
            // must be atomic per agency — hence the write lock, as elsewhere.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            contract = await _documents.GenerateContractAsync(
                new RentalDocumentRequest(request.RentingId, request.TemplateId, request.ManualValues),
                cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                // The PDF was written to storage before the row was committed; a
                // rolled-back insert would otherwise leave the bytes orphaned
                // (same ordering as the car-image upload path).
                if (contract.DocumentFile is { } file)
                {
                    await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);
                }

                throw;
            }

            return _mapper.Map<ContractDto>(contract);
        }
    }
}

namespace RemSolution.Application.Features.Contract.Commands.GenerateContractCommand
{
    public class GenerateContractCommandValidator : AbstractValidator<GenerateContractCommand>
    {
        public GenerateContractCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
        }
    }
}
