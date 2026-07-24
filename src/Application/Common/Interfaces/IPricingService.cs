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
}
