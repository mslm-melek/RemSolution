namespace RemSolution.Application.Common.Notifications;

/// <summary>
/// Whether a recurring car cost is coming due, and on which of the two clocks.
/// </summary>
public enum ExpenseDueBasis
{
    /// <summary>Due because enough months have passed (insurance, road tax, inspection).</summary>
    Date = 1,

    /// <summary>Due because the car has covered enough kilometres (servicing).</summary>
    Distance = 2,
}

/// <summary>
/// One car owing one recurring expense. <see cref="Basis"/> says which clock
/// raised it, and only that clock's figures are populated.
/// </summary>
/// <param name="Basis">Which threshold triggered.</param>
/// <param name="IsOverdue">Past the threshold, as opposed to inside the warning window.</param>
/// <param name="DueOn">Date it falls due (Date basis only).</param>
/// <param name="Days">Whole days until it falls due, or since it did when overdue. Never negative.</param>
/// <param name="DueAtKilometers">Odometer reading it falls due at (Distance basis only).</param>
/// <param name="Kilometers">Kilometres left before it falls due, or covered past it. Never negative.</param>
public sealed record ExpenseDue(
    ExpenseDueBasis Basis,
    bool IsOverdue,
    DateTime? DueOn,
    int Days,
    int? DueAtKilometers,
    int? Kilometers)
{
    /// <summary>The wording this due maps to; see <see cref="NotificationMessages"/>.</summary>
    public string MessageKey => (Basis, IsOverdue) switch
    {
        (ExpenseDueBasis.Date, false) => NotificationMessages.CarExpenseDueByDate,
        (ExpenseDueBasis.Date, true) => NotificationMessages.CarExpenseOverdueByDate,
        (ExpenseDueBasis.Distance, false) => NotificationMessages.CarExpenseDueByDistance,
        _ => NotificationMessages.CarExpenseOverdueByDistance,
    };
}

/// <summary>
/// Reads the agency's recurring-cost rules and says what is coming due.
/// <para>
/// The rules are the ones already on <c>ExpenseType</c>: a type flagged
/// <c>WithNotif</c> recurs every <c>AfterMonth</c> months, or every
/// <c>AfterKilometer</c> kilometres, or both — insurance every 12 months, a
/// service every 10 000 km. The schedule is therefore relative to when that cost
/// was last booked against that car: the most recent expense of the type IS the
/// last time the work was done, so it is the baseline, and the notification the
/// agency gets is the prompt to book the next one.
/// </para>
/// <para>
/// Deliberately pure and static: this is the rule the sweep, the tests and any
/// later "what is due" screen must agree on, and it needs no database to state.
/// </para>
/// </summary>
public static class ExpenseDueCalculator
{
    /// <summary>
    /// Evaluates one (car, expense type) pair.
    /// <para>
    /// Returns null when nothing is owed yet, and — importantly — also when the
    /// question cannot be answered: a car with no expense of this type has no
    /// baseline to count from, so it is silent rather than alarming. That is a
    /// real limitation and the deliberate side to err on. Warning about every car
    /// that has never had an insurance line recorded would fire once per car per
    /// type on the day the feature is switched on, and an inbox that opens full
    /// of noise is one nobody reads afterwards. The first recorded expense starts
    /// the schedule.
    /// </para>
    /// </summary>
    /// <param name="afterMonths">The type's month interval; null/0 disables the date clock.</param>
    /// <param name="afterKilometers">The type's distance interval; null/0 disables the distance clock.</param>
    /// <param name="lastExpenseOn">When this cost was last booked for the car; null = no baseline.</param>
    /// <param name="lastExpenseMileage">The odometer when it was last booked; null = no distance baseline.</param>
    /// <param name="currentMileage">The car's odometer now; null = unknown, so no distance answer.</param>
    /// <param name="now">Evaluation instant (UTC).</param>
    /// <param name="leadDays">How far ahead of the date threshold to warn.</param>
    /// <param name="leadKilometers">How far ahead of the distance threshold to warn.</param>
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

        // Both clocks can be running on the same type (a service every 12 months
        // OR every 10 000 km). Whichever is further along is the one to report:
        // an already-overdue threshold outranks one that is merely approaching,
        // and between two of the same standing the nearer one is the more useful
        // sentence. One alert per (car, type) either way — the agency books one
        // garage visit, not two.
        if (byDate is null) return byDistance;
        if (byDistance is null) return byDate;

        if (byDate.IsOverdue != byDistance.IsOverdue)
        {
            return byDate.IsOverdue ? byDate : byDistance;
        }

        // Same standing: compare how far each is from its threshold, in its own
        // units. Overdue counts up (the bigger overrun is worse), approaching
        // counts down (the smaller margin is more urgent).
        var dateRank = Fraction(byDate.Days, leadDays);
        var distanceRank = Fraction(byDistance.Kilometers ?? 0, leadKilometers);

        return byDate.IsOverdue
            ? (dateRank >= distanceRank ? byDate : byDistance)
            : (dateRank <= distanceRank ? byDate : byDistance);
    }

    // Distance from the threshold as a share of the warning window, so days and
    // kilometres can be compared at all. A zero window (the agency wants no lead
    // time) would divide by zero, so it degrades to the raw figure.
    private static double Fraction(int amount, int window) =>
        window > 0 ? (double)amount / window : amount;

    private static ExpenseDue? EvaluateDate(
        int? afterMonths, DateTime? lastExpenseOn, DateTime now, int leadDays)
    {
        if (afterMonths is not > 0 || lastExpenseOn is not DateTime last)
        {
            return null;
        }

        // AddMonths, not 30-day arithmetic: an insurance premium renewed on the
        // 31st of January is due on the 28th of February, which is what the
        // paperwork says and what a fixed day count would get wrong.
        var dueOn = last.AddMonths(afterMonths.Value);

        // Compared on whole days so a notification raised at 02:00 and the same
        // one raised at 23:00 agree about how many days are left.
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
        // No reading when the work was done, or none for the car today, and the
        // distance clock simply has no answer — see the class remarks. Expenses
        // recorded before the odometer field existed land here.
        if (afterKilometers is not > 0
            || lastExpenseMileage is not int lastMileage
            || currentMileage is not int mileage)
        {
            return null;
        }

        var dueAt = lastMileage + afterKilometers.Value;
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
