namespace RemSolution.Domain.Entities
{
    public class Expense : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int CarId { get; set; }
        public virtual Car? Car { get; set; }
        public int ExpenseTypeId { get; set; }
        public virtual ExpenseType? ExpenseType { get; set; }
        // UTC per the persistence-boundary rule (see docs "Time"); never local.
        // Stamped by the handler from an injected TimeProvider when the caller
        // supplies no date (as CreateCarCommand does).
        public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
        // Denominated in the owning agency's currency, like every other amount
        // (see Money). Null when the expense has no recorded amount.
        public Money? ExpenseAmount { get; set; }
        // How much of ExpenseAmount the agency has actually settled. An expense
        // is money the agency OWES (garage, insurance, fuel…), so its credit is
        // ExpenseAmount − PaidAmount; it is tracked as a running total here
        // rather than as Payment rows, which record client money in the opposite
        // direction. Never negative and never above ExpenseAmount — the
        // settlement command enforces both.
        public Money? PaidAmount { get; set; }
        /// <summary>
        /// The car's odometer when this cost was incurred, in kilometres. This is
        /// what makes a distance-based recurrence work: an oil change every
        /// 10 000 km is only answerable against the reading it was last done at,
        /// so a service recorded without one cannot schedule the next.
        /// <para>
        /// Nullable and defaulted from the car on create (a garage visit is a real
        /// sighting of the odometer, so it also moves <see cref="Car.Mileage"/>
        /// forward). Expenses recorded before this existed have none, which the
        /// due calculator treats as "no distance baseline" rather than as zero.
        /// </para>
        /// </summary>
        public int? Mileage { get; set; }
        // Facture image as a StoredFile FK for schema consistency. The Expense
        // upload flow is not built yet, so nothing populates this today — it is
        // the deferred half of the StoredFile work.
        public int? FactureFileId { get; set; }
        public virtual StoredFile? FactureFile { get; set; }
        public string? Description { get; set; }


    }
}
