using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Infrastructure.Pricing;

// Flat rate × billed days. Deliberately simple and side-effect free; it exists
// as an injectable seam so seasonal/promotional pricing can replace it without
// any Renting/Reservation handler changing. Stateless → registered as a
// singleton.
public sealed class PricingService : IPricingService
{
    public Money CalculateRentalPrice(Car car, DateTime startDate, DateTime endDate)
    {
        Guard.Against.Null(car);

        if (car.DailyRate is not Money dailyRate)
        {
            throw new InvalidOperationException(
                $"Car {car.Id} has no DailyRate set and cannot be priced.");
        }

        if (endDate <= startDate)
        {
            throw new ArgumentException(
                "The rental end date must be after the start date.", nameof(endDate));
        }

        // A started day counts as a full billed day (minimum one). The result
        // keeps the car's currency.
        var billedDays = (int)Math.Ceiling((endDate - startDate).TotalDays);

        return (dailyRate * billedDays).Round();
    }
}
