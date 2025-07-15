
namespace RemSolution.Domain.Entities
{
    public class Brand : BaseAuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public virtual ICollection<ModelCar>? ModelCars { get; set; }

    }
}
