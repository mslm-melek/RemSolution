using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// What to generate and with what: the template to use and the values the agent
/// was prompted for.
/// </summary>
/// <param name="RentingId">The booking the document covers.</param>
/// <param name="TemplateId">
/// The agency template to use. Null falls back to the agency's default for this
/// kind and language, and then to the platform's shipped example — so generation
/// works for an agency that has never opened the template screen.
/// </param>
/// <param name="ManualValues">
/// Values for the template's ask-each-time placeholders, keyed by placeholder
/// name. A required one that is missing fails generation rather than printing a
/// document with a hole in it.
/// </param>
public sealed record RentalDocumentRequest(
    int RentingId,
    int? TemplateId = null,
    IReadOnlyDictionary<string, string>? ManualValues = null);

/// <summary>
/// Issues the generated paperwork for a renting: resolves the template, assigns
/// the next per-agency number, renders the PDF, archives it as a
/// <see cref="StoredFile"/> and tracks the document row.
/// <para>
/// Contract with callers, mirroring <see cref="IStoredFileService"/>:
/// </para>
/// <list type="bullet">
/// <item>Both methods must be called inside a transaction that already holds the
/// per-agency write lock — the number is <c>MAX(SequenceNumber) + 1</c>, which is
/// a race without it. The unique index on (AgencyId, Year, SequenceNumber) is the
/// backstop if that is ever forgotten.</item>
/// <item>Neither calls <c>SaveChanges</c>. The caller commits the document
/// together with whatever else its unit of work is doing, so a renting and its
/// contract are created all-or-nothing.</item>
/// <item>Callers are responsible for the entitlement check
/// (<c>Contract.Generate</c> + the Contracts feature, and the Facture pair):
/// these methods generate, they do not authorize.</item>
/// </list>
/// </summary>
public interface IRentalDocumentService
{
    Task<Contract> GenerateContractAsync(
        RentalDocumentRequest request, CancellationToken cancellationToken = default);

    Task<Facture> GenerateFactureAsync(
        RentalDocumentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ask-each-time placeholders the agent must be prompted for before
    /// generating, for the template that would actually be used. Empty when the
    /// template needs nothing (which is the case for both shipped examples).
    /// </summary>
    Task<IReadOnlyList<DocumentTemplateField>> GetPromptFieldsAsync(
        DocumentTemplateKind kind, int? templateId, CancellationToken cancellationToken = default);
}
