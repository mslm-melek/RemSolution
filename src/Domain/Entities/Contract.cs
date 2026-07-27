namespace RemSolution.Domain.Entities
{
    // The rental agreement generated for a renting: a numbered, archived PDF.
    //
    // The row is metadata ABOUT the document; the document itself is the
    // StoredFile the DocumentFileId points at. Once written, neither the number
    // nor the file is rewritten — regenerating (e.g. after extra services were
    // added) produces a NEW contract row with the next number, so the paper the
    // client signed always stays retrievable. Contracts are legal records and
    // are never deleted, which is why this is not ISoftDeletable.
    public class Contract : BaseAuditableEntity, ITenantEntity, INumberedDocument
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        // The renting the agreement covers. Required: a contract without its
        // booking is meaningless.
        public int RentingId { get; set; }
        public virtual Renting? Renting { get; set; }

        // Per-agency, per-year sequence; see INumberedDocument.
        public int Year { get; set; }
        public int SequenceNumber { get; set; }
        public string Number { get; set; } = string.Empty;

        // When the agreement was issued (UTC), which is what the document prints
        // — deliberately separate from the CreatedOn audit stamp.
        public DateTime IssuedAt { get; set; }

        // The rendered PDF. Required, and never reassigned: the bytes ARE the
        // contract.
        public int DocumentFileId { get; set; }
        public virtual StoredFile? DocumentFile { get; set; }

        // Neutral language tag the document was rendered in ("fr"/"ar"/"en"), so
        // a re-download serves the copy the client was actually handed rather
        // than silently re-rendering in the reader's current language.
        public string Language { get; set; } = string.Empty;

        // Which template produced this. Null means the platform's shipped example
        // was used (those are code, not rows — see DocumentTemplate). The name is
        // snapshotted because the template may be edited or retired afterwards:
        // the id answers "which template", the name answers "what was it called
        // at the time", and the PDF remains the authority on the content.
        public int? DocumentTemplateId { get; set; }
        public virtual DocumentTemplate? DocumentTemplate { get; set; }
        public string? TemplateName { get; set; }
    }
}
