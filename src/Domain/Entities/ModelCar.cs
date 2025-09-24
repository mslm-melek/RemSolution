namespace RemSolution.Domain.Entities
{
    public class ModelCar : BaseAuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public int? BrandId { get; set; }
        public virtual Brand? Brand { get; set; }
        public virtual ICollection<Car>? Cars { get; set; }
        public override string ToString()
        {
            var model = Brand?.Name ?? "Unknown Brand";

            return $"{model} {Name}";
        }

    }
}
