namespace RemSolution.Domain.Entities
{
    public class Client : BaseAuditableEntity, ITenantEntity, IHasRowVersion, ISoftDeletable
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        // Archived rather than deleted; see ISoftDeletable.
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? BirthPlace { get; set; }
        public int? BirthCountryId { get; set; }
        public virtual Country? BirthCountry { get; set; }
        public string? CIN { get; set; }
        public DateTime? CINDeliveranceDate { get; set; }
        public string? CINDeliverancePlace { get; set; }
        public int? CINDeliveranceCountryId { get; set; }
        public virtual Country? CINDeliveranceCountry { get; set; }
        public string? PasseportNumber { get; set; }
        public DateTime? PasseportDeliveranceDate { get; set; }
        public string? PasseportDeliverancePlace { get; set; }
        public int? PasseportDeliveranceCountryId { get; set; }
        public virtual Country? PasseportDeliveranceCountry { get; set; }
        public string? DrivingLicenceNumber { get; set; }
        public DateTime? DrivingLicenceDeliveranceDate { get; set; }
        public string? DrivingLicenceDeliverancePlace { get; set; }
        public int? DrivingLicenceDeliveranceCountryId { get; set; }
        public virtual Country? DrivingLicenceDeliveranceCountry { get; set; }
        // Identity-document images are StoredFile records, not raw URL strings:
        // size/mime/SHA-256/uploader travel with each file. Managed solely by
        // UploadClientDocumentCommand; the ClientDto still surfaces the plain URL.
        public int? CINFileId { get; set; }
        public virtual StoredFile? CINFile { get; set; }
        public int? DrivingLicenceFileId { get; set; }
        public virtual StoredFile? DrivingLicenceFile { get; set; }
        public int? PasseportFileId { get; set; }
        public virtual StoredFile? PasseportFile { get; set; }
        public string? Description { get; set; }
        // Per-agency bad-client flag: a risk signal raised by the owning agency,
        // with free-text Notes carrying the reason. Deliberately NOT a
        // cross-agency blacklist — the flag lives on this tenant's Client row
        // and never leaks across agencies (the row is already tenant-scoped by
        // AgencyId + global query filters). Notes are internal moderation text,
        // distinct from the client-facing Description. Set only via
        // FlagClientCommand, which audits the change.
        public bool IsFlagged { get; set; }
        public string? Notes { get; set; }
        // Phase 6 marketplace: a self-registered marketplace user has one
        // global account and a linked Client row per agency; null for
        // agency-created clients.
        public string? MarketplaceUserId { get; set; }
        public virtual ICollection<Renting>? Rentings { get; set; }
        public virtual ICollection<Renting>? SecondRentings { get; set; }
        public virtual ICollection<Reservation>? Reservations { get; set; }
        public virtual ICollection<Payment>? Payments { get; set; }


    }
}
