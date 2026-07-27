using RemSolution.Application.Common.Documents;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Draws a resolved document as PDF bytes. Pure and synchronous: no database, no
/// storage, no clock, no locale lookups — everything it prints is already a
/// string on the <see cref="RenderedDocument"/>. That is what makes a rendered
/// document reproducible from its model alone, and testable without either.
/// <para>
/// One method, not one per document kind: since templates arrived, a contract and
/// an invoice differ only in their blocks, so a second entry point would just be
/// two names for the same drawing code.
/// </para>
/// </summary>
public interface IRentalDocumentRenderer
{
    byte[] Render(RenderedDocument document);
}
