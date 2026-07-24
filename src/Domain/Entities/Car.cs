namespace RemSolution.Domain.Entities
{
    public class Car : BaseAuditableEntity, ITenantEntity, IHasRowVersion, ISoftDeletable
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        // Archived rather than deleted; see ISoftDeletable.
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public string? Matricule { get; set; }
        public int? ModelId { get; set; }
        public virtual ModelCar? Model { get; set; }
        // The car's home branch — the geographic anchor availability and search
        // are scoped by. Nullable: legacy cars predate branches, and a branch
        // delete clears it (SetNull) rather than removing the car.
        public int? BranchId { get; set; }
        public virtual Branch? Branch { get; set; }
        // Only Active cars are bookable; Maintenance/Inactive are hidden from
        // availability. Defaults to Active for new cars.
        public CarStatus Status { get; set; } = CarStatus.Active;
        // The car's current rental rate per day. IPricingService reads this to
        // compute the price snapshotted onto a Renting/Reservation at creation
        // time; nullable because an unpriced car cannot yet be booked. Carries
        // its own currency (the agency's), so the computed price does too.
        public Money? DailyRate { get; set; }
        public DateTime FirstCirculationDate { get; set; }
        public string? Color { get; set; }
        // The car photo is a StoredFile record (managed by UploadCarPhotoCommand),
        // not a raw URL string; the CarDto still surfaces the plain URL.
        public int? PhotoFileId { get; set; }
        public virtual StoredFile? PhotoFile { get; set; }
        // Multi-image gallery (CarImage) superseding the single PhotoFile; each
        // entry carries its own original + generated thumbnail/medium.
        public virtual ICollection<CarImage>? Images { get; set; }
        public int? Power { get; set; }
        public FuelType? FuelType { get; set; }
        public virtual ICollection<Expense>? Expenses { get; set; }
        public virtual ICollection<Renting>? Rentings { get; set; }
        public override string ToString()
        {
            var model = Model?.ToString()?? "Unknown Model";
            var matricule = Matricule ?? "No Matricule";

            return $"{model} - {matricule}";
        }
    }
}
