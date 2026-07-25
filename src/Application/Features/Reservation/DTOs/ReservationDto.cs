using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Reservation.DTOs
{
    public class ReservationDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        // Optimistic-concurrency token; echoed back on update (see P.8).
        public byte[]? RowVersion { get; init; }
        public int? CarId { get; init; }
        public string? CarMatricule { get; init; }
        public string? CarModelName { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public MoneyDto? Price { get; init; }
        public MoneyDto? PayedPrice { get; init; }
        public string? Notes { get; init; }
        public ReservationStatus Status { get; init; }
        public DateTime? ExpiresAt { get; init; }
        // Set once the hold is confirmed into a renting.
        public int? RentingId { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Reservation, ReservationDto>()
                      .Map(dest => dest.CarMatricule, src => src.Car != null ? src.Car.Matricule : null)
                      .Map(dest => dest.CarModelName,
                           src => src.Car != null && src.Car.Model != null ? src.Car.Model.Name : null)
                      .Map(dest => dest.ClientName,
                           src => src.Client != null ? src.Client.FirstName + " " + src.Client.LastName : null);
            }
        }
    }
}
