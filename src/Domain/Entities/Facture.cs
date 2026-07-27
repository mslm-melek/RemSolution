namespace RemSolution.Domain.Entities
{
    // The client invoice generated for a renting: a numbered, archived PDF.
    //
    // Same immutability rule as Contract — an issued invoice is never edited or
    // deleted; a correction is a new invoice with the next number. The line
    // detail lives in the rendered PDF (the artifact the client holds); this row
    // keeps only the totals, so an invoice list or a balance report never has to
    // re-open a PDF to answer "how much".
    public class Facture : BaseAuditableEntity, ITenantEntity, INumberedDocument
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int RentingId { get; set; }
        public virtual Renting? Renting { get; set; }

        // Denormalised from the renting so an invoice list can filter by client
        // without joining, and so the billed party stays recorded even if the
        // renting's client is later corrected.
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }

        // Per-agency, per-year sequence; see INumberedDocument.
        public int Year { get; set; }
        public int SequenceNumber { get; set; }
        public string Number { get; set; } = string.Empty;

        public DateTime IssuedAt { get; set; }

        // Totals snapshotted at issue time, in the agency's currency: the rental
        // charge, the extra services billed alongside it, and their sum. Stored
        // rather than recomputed because later edits to the renting must not
        // change what an issued invoice says.
        public Money? RentalAmount { get; set; }
        public Money? ExtraServicesAmount { get; set; }
        public Money? TotalAmount { get; set; }

        public int DocumentFileId { get; set; }
        public virtual StoredFile? DocumentFile { get; set; }

        // See Contract.Language.
        public string Language { get; set; } = string.Empty;

        // See Contract.DocumentTemplateId.
        public int? DocumentTemplateId { get; set; }
        public virtual DocumentTemplate? DocumentTemplate { get; set; }
        public string? TemplateName { get; set; }
    }
}
