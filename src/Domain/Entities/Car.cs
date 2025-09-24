namespace RemSolution.Domain.Entities
{
    public class Car : BaseAuditableEntity
    {
        public string? Matricule { get; set; }
        public int? ModelId { get; set; }
        public virtual ModelCar? Model { get; set; }
        public DateTime FirstCirculationDate { get; set; }
        public string? Color { get; set; }
        public string? ImageUrl { get; set; }
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
