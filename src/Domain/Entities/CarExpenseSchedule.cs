namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One car's own figures for a notifiable <see cref="ExpenseType"/> — the same
    /// service can be every 8 000 km on one van and 10 000 km on another. Every
    /// field is nullable and null falls back to the type or the agency setting, so
    /// linking a car and changing nothing is harmless.
    /// </summary>
    public class CarExpenseSchedule : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int CarId { get; set; }
        public virtual Car? Car { get; set; }

        public int ExpenseTypeId { get; set; }
        public virtual ExpenseType? ExpenseType { get; set; }

        /// <summary>Distance between two of these jobs on this car; null = the type's.</summary>
        public int? AfterKilometer { get; set; }

        /// <summary>Months between two of these jobs on this car; null = the type's.</summary>
        public int? AfterMonth { get; set; }

        /// <summary>How far ahead to warn, in km; null = the agency's setting.</summary>
        public int? LeadKilometers { get; set; }

        /// <summary>How far ahead to warn, in days; null = the agency's setting.</summary>
        public int? LeadDays { get; set; }

        /// <summary>Odometer when this was last done, as declared rather than invoiced.</summary>
        public int? LastDoneMileage { get; set; }

        /// <summary>When this was last done, as declared rather than invoiced.</summary>
        public DateTime? LastDoneOn { get; set; }
    }
}
