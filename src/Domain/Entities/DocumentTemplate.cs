namespace RemSolution.Domain.Entities
{
    // An agency's own layout for a contract or an invoice: an ordered list of
    // blocks whose text carries {{placeholders}}, plus one binding row per
    // placeholder saying where its value comes from.
    //
    // Templates are ordinary tenant data. The platform's read-only EXAMPLES are
    // deliberately NOT rows here: they are defined in code
    // (DocumentTemplateExamples) and localized from the shared .resx, which keeps
    // the tenancy model intact (no nullable-AgencyId rows that every query would
    // have to remember to include) and means improving a shipped example reaches
    // every agency that has not cloned it yet. Cloning materialises an example
    // into a row of this table, owned and editable by the agency.
    public class DocumentTemplate : BaseAuditableEntity, ITenantEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public string Name { get; set; } = string.Empty;

        public DocumentTemplateKind Kind { get; set; }

        // Neutral language tag the template's text is written in. A template is
        // written in ONE language: the clauses are legal text, not UI strings, so
        // an agency serving two languages keeps two templates rather than one
        // template with a translation table.
        public string Language { get; set; } = string.Empty;

        // The template picked when the agent does not choose one, per
        // (Kind, Language). Enforced as at most one default by
        // SetDefaultDocumentTemplateCommand, not by a database constraint —
        // a filtered unique index would make "swap the default" a two-statement
        // dance that could leave the agency with none.
        public bool IsDefault { get; set; }

        // Retired templates stay for the audit trail (an issued document names
        // the template that produced it) but disappear from the pickers.
        public bool IsActive { get; set; } = true;

        // The blocks, as JSON. Stored as a document rather than a table because a
        // template is only ever read and written whole, and the block schema is
        // the application's business — nothing queries inside it. See
        // DocumentBlock / DocumentTemplateBlocks.
        public string BlocksJson { get; set; } = string.Empty;

        // The placeholder bindings ARE a table: the generation flow queries them
        // ("what must I prompt for?") and the editor validates against them.
        public virtual ICollection<DocumentTemplateField>? Fields { get; set; }
    }
}
