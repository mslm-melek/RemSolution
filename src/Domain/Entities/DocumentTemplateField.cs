namespace RemSolution.Domain.Entities
{
    // Where one placeholder in a template gets its value. One row per DISTINCT
    // placeholder name, however many times it appears in the blocks.
    public class DocumentTemplateField : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int DocumentTemplateId { get; set; }
        public virtual DocumentTemplate? DocumentTemplate { get; set; }

        // The placeholder name as it appears between the braces, without them:
        // "client.fullName", "franchise".
        public string Placeholder { get; set; } = string.Empty;

        public DocumentFieldBinding Binding { get; set; }

        // DataField only: a path from DocumentPlaceholders. Usually equal to
        // Placeholder (that is what makes auto-binding possible), but an admin can
        // point a placeholder called anything at any known path.
        public string? DataPath { get; set; }

        // FixedValue only.
        public string? FixedValue { get; set; }

        // AskEachTime only: what the agent sees above the input. Falls back to the
        // placeholder name when empty.
        public string? Label { get; set; }

        // AskEachTime only: generation fails rather than printing a document with
        // a hole in it.
        public bool IsRequired { get; set; }
    }
}
