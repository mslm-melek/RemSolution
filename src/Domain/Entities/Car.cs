namespace RemSolution.Domain.Entities
{
    public class Car : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public string? Matricule { get; set; }
        public int? ModelId { get; set; }
        public virtual ModelCar? Model { get; set; }
        public DateTime FirstCirculationDate { get; set; }
        public string? Color { get; set; }
        // The car photo is a StoredFile record (managed by UploadCarPhotoCommand),
        // not a raw URL string; the CarDto still surfaces the plain URL.
        public int? PhotoFileId { get; set; }
        public virtual StoredFile? PhotoFile { get; set; }
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
