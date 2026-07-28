using RemSolution.Domain.Entities;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// The single seam that turns a car's current rate into the price stored on a
/// booking. Renting/Reservation create handlers call this once, at creation
/// time, and persist the result — so a booking keeps its agreed price even
/// after the car's <see cref="Car.DailyRate"/> is later changed. Centralising
/// the calculation here is what makes that snapshot rule enforceable: no
/// handler multiplies a rate by a duration on its own.
/// </summary>
public interface IPricingService
{
    /// <summary>
    /// Computes the snapshot price for renting <paramref name="car"/> over the
    /// half-open period [<paramref name="startDate"/>, <paramref name="endDate"/>).
    /// A started day is billed in full (minimum one day). Throws
    /// <see cref="InvalidOperationException"/> if the car has no
    /// <see cref="Car.DailyRate"/> (it is not yet priced and cannot be booked)
    /// and <see cref="ArgumentException"/> if the period is not positive. The
    /// result carries the car's currency (from its DailyRate).
    /// </summary>
    Money CalculateRentalPrice(Car car, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Re-prices a booking whose end date moved, without re-opening the price of
    /// the days the client already agreed to.
    /// <list type="bullet">
    /// <item>Days ADDED are new business, quoted at the car's current
    /// <see cref="Car.DailyRate"/>.</item>
    /// <item>Days GIVEN BACK are credited at the rate this booking was agreed at
    /// (<paramref name="agreedPrice"/> spread over its own billed days), never at
    /// today's rate — otherwise an early return could credit more than was
    /// charged after a rate rise.</item>
    /// <item>A new end date inside the same billed day returns
    /// <paramref name="agreedPrice"/> untouched.</item>
    /// </list>
    /// That is the difference from <see cref="CalculateRentalPrice"/>, which
    /// quotes a whole period from scratch: extending a booking must not silently
    /// re-price the part already agreed (see the snapshot rule above).
    /// Throws <see cref="InvalidOperationException"/> when days are added and the
    /// car has no rate, and <see cref="ArgumentException"/> if either end date is
    /// not after <paramref name="startDate"/>.
    /// </summary>
    Money RepriceForNewEndDate(
        Car car, Money agreedPrice, DateTime startDate, DateTime originalEndDate, DateTime newEndDate);
}
