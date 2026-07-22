using RemSolution.Domain.Common;
using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Entities
{
    // Rich metadata for a stored binary (identity documents, car photos, expense
    // invoices). The originating entity holds a FK to this row instead of a raw
    // URL string, so size/mime/hash/uploader travel with the file. Originals are
    // kept at full resolution for later mobile OCR.
    //
    // The SHA-256 hash powers two things: per-agency deduplication (identical
    // bytes are written to disk once, and rows reuse the same physical Path) and
    // the PII/legal trail. Tenant-scoped, so dedup and access never cross an
    // agency boundary.
    //
    // One row per upload: OriginalFileName, DocumentType and UploadedBy/At are
    // per-upload facts, so identical bytes still get their own row — they merely
    // share Path/Url. UploadedBy/At are the audit CreatedBy/CreatedOn on
    // BaseAuditableEntity, stamped by the auditing interceptor.
    public class StoredFile : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        // Storage-relative path of the physical bytes; shared by rows that dedup
        // to the same content within an agency.
        public string Path { get; set; } = string.Empty;

        // Public URL previously returned by IFileStorage.SaveAsync.
        public string Url { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long Size { get; set; }

        // Lowercase hex SHA-256 of the file bytes.
        public string Sha256 { get; set; } = string.Empty;

        public DocumentType DocumentType { get; set; }
    }
}
