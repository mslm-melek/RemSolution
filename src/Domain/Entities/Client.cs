namespace RemSolution.Domain.Entities
{
    public class Client : BaseAuditableEntity
    {
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
        public string? CINImageUrl { get; set; }
        public string? DrivingLicenceImageUrl { get; set; }
        public string? PasserportImageUrl { get; set; }
        public string? Description { get; set; }
        public virtual ICollection<Renting>? Rentings { get; set; }
        public virtual ICollection<Renting>? SecondRentings { get; set; }
        public virtual ICollection<Reservation>? Reservations { get; set; }
        public virtual ICollection<Payment>? Payments { get; set; }


    }
}
