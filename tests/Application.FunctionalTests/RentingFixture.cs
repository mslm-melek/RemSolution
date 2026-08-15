using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests;

/// <summary>
/// Builds a <see cref="Renting"/> in whatever state a fixture needs, through the
/// aggregate's own factory and transitions.
/// </summary>
internal static class RentingFixture
{
    // A hire cannot exist without a period, so one is supplied either way.
    public static readonly DateTime DefaultStart = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime DefaultEnd = new(2030, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    public static Renting Hire(
        int carId,
        int clientId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        RentingState state = RentingState.NotYet,
        Money? price = null,
        int? startMileage = null,
        int? endMileage = null,
        int? secondClientId = null,
        Money? depositAmount = null,
        Money? cancellationFee = null,
        string? notes = null)
    {
        var renting = Renting.Create(
            carId: carId,
            clientId: clientId,
            startDate: startDate ?? DefaultStart,
            endDate: endDate ?? DefaultEnd,
            price: price,
            startMileage: startMileage,
            secondClientId: secondClientId,
            depositAmount: depositAmount,
            notes: notes);

        switch (state)
        {
            case RentingState.InProgress:
                renting.Start();
                break;

            case RentingState.Done:
                renting.Start();
                renting.Complete(endMileage, renting.EndDate!.Value);
                break;

            case RentingState.Cancelled:
                renting.Cancel(cancellationFee);
                break;
        }

        return renting;
    }
}
