namespace RemSolution.Application.Features.Car.DTOs
{
    // Just enough of a hire for a car row or a car page to talk about it: who has
    // the car, for how long, and the odometer it left on (the floor a return
    // reading has to clear). Anything more belongs to the renting screen.
    public class CarRentingSummaryDto
    {
        public int Id { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public int? StartMileage { get; init; }
    }
}
