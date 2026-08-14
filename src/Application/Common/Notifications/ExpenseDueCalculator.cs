namespace RemSolution.Application.Common.Notifications;

/// <summary>Which clock says a recurring car cost is coming due.</summary>
public enum ExpenseDueBasis
{
    /// <summary>Enough months have passed (insurance, road tax, inspection).</summary>
    Date = 1,

    /// <summary>Enough kilometres have been covered (servicing).</summary>
    Distance = 2,
}

/// <summary>
/// One car owing one recurring expense. Only the triggering clock's figures are set.
/// </summary>
public sealed record ExpenseDue(
    ExpenseDueBasis Basis,
    bool IsOverdue,
    DateTime? DueOn,
    int Days,
    int? DueAtKilometers,
    int? Kilometers)
{
    /// <summary>The wording this due maps to.</summary>
    public string MessageKey => (Basis, IsOverdue) switch
    {
        (ExpenseDueBasis.Date, false) => NotificationMessages.CarExpenseDueByDate,
        (ExpenseDueBasis.Date, true) => NotificationMessages.CarExpenseOverdueByDate,
        (ExpenseDueBasis.Distance, false) => NotificationMessages.CarExpenseDueByDistance,
        _ => NotificationMessages.CarExpenseOverdueByDistance,
    };
}

/// <summary>
/// Says what recurring car cost is coming due, from the intervals on
/// <c>ExpenseType</c> counted off the last expense booked for the car.
/// Pure and static so the sweep, the screens and the tests share one rule.
/// </summary>
public static class ExpenseDueCalculator
{
    /// <summary>
    /// Evaluates one (car, expense type) pair. Null when nothing is owed, and also
    /// when there is no baseline to count from — silence beats alarming every car
    /// on the day the feature is switched on.
    /// </summary>
    public static ExpenseDue? Evaluate(
        int? afterMonths,
        int? afterKilometers,
        DateTime? lastExpenseOn,
        int? lastExpenseMileage,
        int? currentMileage,
        DateTime now,
        int leadDays,
        int leadKilometers)
    {
        var byDate = EvaluateDate(afterMonths, lastExpenseOn, now, leadDays);
        var byDistance = EvaluateDistance(
            afterKilometers, lastExpenseMileage, currentMileage, leadKilometers);

        // Both clocks can run on one type. Report the one further along — one alert
        // per (car, type), because the agency books one garage visit.
        if (byDate is null) return byDistance;
        if (byDistance is null) return byDate;

        if (byDate.IsOverdue != byDistance.IsOverdue)
        {
            return byDate.IsOverdue ? byDate : byDistance;
        }

        // Same standing: compare each margin as a share of its own warning window.
        var dateRank = Fraction(byDate.Days, leadDays);
        var distanceRank = Fraction(byDistance.Kilometers ?? 0, leadKilometers);

        return byDate.IsOverdue
            ? (dateRank >= distanceRank ? byDate : byDistance)
            : (dateRank <= distanceRank ? byDate : byDistance);
    }

    /// <summary>
    /// When the next one falls due, or null when the date clock cannot run. Public
    /// so a screen can quote the date through the same arithmetic as the warning.
    /// </summary>
    public static DateTime? NextDueOn(DateTime? lastDoneOn, int? afterMonths) =>
        // AddMonths, not 30-day arithmetic: a 31 January renewal is due 28 February.
        afterMonths is > 0 && lastDoneOn is DateTime last
            ? last.AddMonths(afterMonths.Value)
            : null;

    /// <summary>The odometer the next one falls due at, or null when the distance clock cannot run.</summary>
    public static int? NextDueAtKilometers(int? lastDoneMileage, int? afterKilometers) =>
        afterKilometers is > 0 && lastDoneMileage is int last
            ? last + afterKilometers.Value
            : null;

    // Margin as a share of the warning window, so days and kilometres compare.
    // A zero window would divide by zero, so it degrades to the raw figure.
    private static double Fraction(int amount, int window) =>
        window > 0 ? (double)amount / window : amount;

    private static ExpenseDue? EvaluateDate(
        int? afterMonths, DateTime? lastExpenseOn, DateTime now, int leadDays)
    {
        if (NextDueOn(lastExpenseOn, afterMonths) is not DateTime dueOn)
        {
            return null;
        }

        // Whole days, so the same due reads the same at 02:00 and at 23:00.
        var days = (int)Math.Round((dueOn.Date - now.Date).TotalDays);

        if (days > Math.Max(leadDays, 0))
        {
            return null;
        }

        return new ExpenseDue(
            ExpenseDueBasis.Date,
            IsOverdue: days < 0,
            DueOn: dueOn,
            Days: Math.Abs(days),
            DueAtKilometers: null,
            Kilometers: null);
    }

    private static ExpenseDue? EvaluateDistance(
        int? afterKilometers, int? lastExpenseMileage, int? currentMileage, int leadKilometers)
    {
        // No reading when it was done, or none today: the distance clock has no answer.
        if (NextDueAtKilometers(lastExpenseMileage, afterKilometers) is not int dueAt
            || currentMileage is not int mileage)
        {
            return null;
        }

        var remaining = dueAt - mileage;

        if (remaining > Math.Max(leadKilometers, 0))
        {
            return null;
        }

        return new ExpenseDue(
            ExpenseDueBasis.Distance,
            IsOverdue: remaining < 0,
            DueOn: null,
            Days: 0,
            DueAtKilometers: dueAt,
            Kilometers: Math.Abs(remaining));
    }
}
