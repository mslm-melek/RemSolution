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

        // The result keeps the car's currency.
        return (dailyRate * BilledDays(startDate, endDate, nameof(endDate))).Round();
    }

    public Money RepriceForNewEndDate(
        Car car, Money agreedPrice, DateTime startDate, DateTime originalEndDate, DateTime newEndDate)
    {
        Guard.Against.Null(car);
        Guard.Against.Null(agreedPrice);

        var originalDays = BilledDays(startDate, originalEndDate, nameof(originalEndDate));
        var newDays = BilledDays(startDate, newEndDate, nameof(newEndDate));

        // The new date lands inside the same billed day: nothing was bought and
        // nothing given back, so the agreed price stands.
        if (newDays == originalDays)
        {
            return agreedPrice;
        }

        if (newDays > originalDays)
        {
            if (car.DailyRate is not Money dailyRate)
            {
                throw new InvalidOperationException(
                    $"Car {car.Id} has no DailyRate set, so the extension cannot be priced.");
            }

            // Only the extra days are quoted at today's rate; the agreed part is
            // carried over untouched. (Money.Add rejects a currency mismatch,
            // which cannot arise inside one agency — it has a single currency.)
            return (agreedPrice + dailyRate * (newDays - originalDays)).Round();
        }

        // Shortened. The credit comes out of what was agreed, pro rata over the
        // days that agreement covered — deliberately NOT the car's current rate,
        // which may have moved since the booking was made.
        return (agreedPrice * ((decimal)newDays / originalDays)).Round();
    }

    // A started day counts as a full billed day (minimum one).
    private static int BilledDays(DateTime startDate, DateTime endDate, string endParameterName)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException(
                "The rental end date must be after the start date.", endParameterName);
        }

        return (int)Math.Ceiling((endDate - startDate).TotalDays);
    }
}
