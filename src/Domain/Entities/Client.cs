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
        // Each identity document records number, when it was issued, where, and
        // — see the expiry dates below — until when.
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

        // Wall-clock dates (a calendar day, stored as picked — see the UTC note
        // in docs/PROJECT_OVERVIEW.md). Null means "not recorded", which is not
        // the same as valid: an unknown expiry never blocks a hire, it just
        // cannot vouch for one either.
        //
        // The licence date is the one with money behind it. Handing keys to a
        // driver whose licence has lapsed lets the insurer decline the claim,
        // and the liability lands on the agency — so ClientDocumentValidity
        // reads these at the moment a hire is created, not only on the client
        // screen. See DrivingLicenceExpiringSoon in NotificationKind for the
        // heads-up that arrives before it comes to that.
        public DateTime? CINExpiryDate { get; set; }
        public DateTime? PasseportExpiryDate { get; set; }
        public DateTime? DrivingLicenceExpiryDate { get; set; }
        // Identity-document images are StoredFile records, not raw URL strings:
        // size/mime/SHA-256/uploader travel with each file. Managed solely by
        // UploadClientDocumentCommand; the ClientDto still surfaces the plain URL.
        public int? CINFileId { get; set; }
        public virtual StoredFile? CINFile { get; set; }
        public int? DrivingLicenceFileId { get; set; }
        public virtual StoredFile? DrivingLicenceFile { get; set; }
        public int? PasseportFileId { get; set; }
        public virtual StoredFile? PasseportFile { get; set; }
        // The client's face, cut out of the CIN image above and squared off, so a
        // list row can show who the client is rather than only what their name
        // is. Purely DERIVED from CINFile: written whenever that file is stored
        // (see UploadClientDocumentCommand) and re-derivable at any time from it
        // (see RegenerateClientPortraitCommand), so losing it loses nothing.
        // Null while there is no CIN image, or when no face could be located on
        // it (a PDF scan, a photo of the back of the card) — the UI falls back to
        // the client's initials.
        public int? CINPortraitFileId { get; set; }
        public virtual StoredFile? CINPortraitFile { get; set; }
        // The client's email address, and the hinge of the customer-account
        // link below: it is both a contact detail and the login of the
        // portal account provisioned for them (see MarketplaceUserId).
        // Not unique — the same household address may appear on several
        // clients, and the same person is a separate Client row per agency.
        public string? Email { get; set; }
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
        // The customer-portal account this client signs in with — one global
        // Identity user (Roles.Customer) with a linked Client row per agency.
        // The link is set two ways, and both land on the same kind of account:
        //   - the customer self-registered on the marketplace and booked here;
        //   - the agency recorded an Email on this client, so an account was
        //     provisioned for them (see IClientAccountService).
        // Null while the client has no email / no account. The link is what
        // every "my …" marketplace query filters on, so it is the single
        // answer to "which portal user is this client?".
        public string? MarketplaceUserId { get; set; }
        /// <summary>
        /// Whether the driving licence is known to have lapsed by
        /// <paramref name="on"/>. An expiry date is exclusive of the day itself —
        /// a licence "valid until the 14th" is good for the whole of the 14th —
        /// so only a date strictly before the day counts as expired.
        /// <para>
        /// False when no expiry is recorded. That is not the same as valid, and it
        /// is deliberately not treated as expired either: refusing every hire to
        /// every client whose paperwork predates this field would take the agency
        /// off the road, and a guess is worse than an honest silence.
        /// </para>
        /// <para>
        /// Only the licence has a method: it is the one an expiry actually
        /// forbids something on (see CreateRentingCommand). The equivalent rule
        /// for all three is spelled out again as a SQL expression in ClientDto,
        /// which is projected and cannot call this.
        /// </para>
        /// </summary>
        public bool IsDrivingLicenceExpiredOn(DateTime on) =>
            DrivingLicenceExpiryDate is DateTime expiry && expiry.Date < on.Date;

        public virtual ICollection<Renting>? Rentings { get; set; }
        public virtual ICollection<Renting>? SecondRentings { get; set; }
        public virtual ICollection<Reservation>? Reservations { get; set; }
        public virtual ICollection<Payment>? Payments { get; set; }


    }
}
