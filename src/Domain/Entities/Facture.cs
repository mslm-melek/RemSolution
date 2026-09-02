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
        // charge, the extra services billed alongside it, the charges the return
        // established (see RentingFee), and their sum. Stored rather than
        // recomputed because later edits to the renting must not change what an
        // issued invoice says.
        public Money? RentalAmount { get; set; }
        public Money? ExtraServicesAmount { get; set; }
        public Money? FeesAmount { get; set; }

        /// <summary>
        /// The sum of the three above, TAX-INCLUSIVE (see AgencySettings' tax
        /// section). This is the amount the lines add up to, not the amount the
        /// client hands over — that is <see cref="TotalDue"/>, which adds the
        /// duty stamp.
        /// </summary>
        public Money? TotalAmount { get; set; }

        // ------------------------------------------------------------------
        // The tax breakdown, computed once at issue and never recomputed. A VAT
        // rate is changed by law and a duty stamp by budget; an invoice reprinted
        // next year has to keep saying what was actually charged, so the rate and
        // the agency's tax number are frozen here alongside the amounts rather
        // than read back from AgencySettings.
        // ------------------------------------------------------------------

        /// <summary>The rate applied, as a percentage (19 means 19%).</summary>
        public decimal VatRatePercent { get; set; }

        /// <summary>Net of tax — <see cref="TotalAmount"/> divided out.</summary>
        public Money? NetAmount { get; set; }

        /// <summary>
        /// The tax itself. Deliberately stored rather than derived: it is
        /// TotalAmount − NetAmount exactly, so no reader can reproduce a
        /// different rounding than the one printed.
        /// </summary>
        public Money? VatAmount { get; set; }

        /// <summary>The fixed duty stamp, as it stood when this was issued.</summary>
        public Money? FiscalStampAmount { get; set; }

        /// <summary>
        /// What the client owes: <see cref="TotalAmount"/> plus the stamp. The one
        /// figure on the document a person acts on.
        /// </summary>
        public Money? TotalDue { get; set; }

        /// <summary>
        /// The agency's tax registration number as printed on this invoice.
        /// Snapshotted for the same reason as the rate: an agency that corrects
        /// its number must not silently rewrite the invoices already issued.
        /// </summary>
        public string? TaxIdentifier { get; set; }

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
