namespace RemSolution.Application.Common.Notifications;

/// <summary>An expense type's fleet-wide recurrence.</summary>
public sealed record CarExpenseTypeRule(
    int ExpenseTypeId,
    int? AfterMonths,
    int? AfterKilometers);

/// <summary>What a car was last charged for a type, folded from its expenses.</summary>
public sealed record CarExpenseBaseline(
    int CarId,
    int ExpenseTypeId,
    DateTime? LastExpenseOn,
    int? LastExpenseMileage);

/// <summary>A car's own figures for a type; null = follow the type.</summary>
public sealed record CarExpenseScheduleValues(
    int CarId,
    int ExpenseTypeId,
    int? AfterMonths,
    int? AfterKilometers,
    int? LeadDays,
    int? LeadKilometers,
    DateTime? LastDoneOn,
    int? LastDoneMileage);

/// <summary>One (car, type) pair with every fallback applied.</summary>
public sealed record CarExpensePlan(
    int CarId,
    int ExpenseTypeId,
    int? AfterMonths,
    int? AfterKilometers,
    int LeadDays,
    int LeadKilometers,
    DateTime? LastDoneOn,
    int? LastDoneMileage,
    bool IsCustom);

/// <summary>
/// Resolves the type rule, the car's overrides and its expense history into one
/// schedule per (car, type). Shared by the sweep and the screens so they agree.
/// </summary>
public static class CarExpenseSchedules
{
    /// <summary>Resolves every pair worth evaluating; pairs with no interval are dropped.</summary>
    public static IReadOnlyList<CarExpensePlan> Plan(
        IEnumerable<CarExpenseTypeRule> types,
        IEnumerable<CarExpenseBaseline> baselines,
        IEnumerable<CarExpenseScheduleValues> schedules,
        int agencyLeadDays,
        int agencyLeadKilometers)
    {
        var rules = types.ToDictionary(t => t.ExpenseTypeId);
        var byPair = baselines
            .Where(b => rules.ContainsKey(b.ExpenseTypeId))
            .ToDictionary(b => (b.CarId, b.ExpenseTypeId));
        var overrides = schedules
            .Where(s => rules.ContainsKey(s.ExpenseTypeId))
            .ToDictionary(s => (s.CarId, s.ExpenseTypeId));

        var plans = new List<CarExpensePlan>();

        // A car is on a schedule if it has an expense of the type or a row of its own.
        foreach (var pair in byPair.Keys.Union(overrides.Keys))
        {
            byPair.TryGetValue(pair, out var baseline);
            overrides.TryGetValue(pair, out var custom);

            var plan = Resolve(
                pair.CarId, rules[pair.ExpenseTypeId], baseline, custom,
                agencyLeadDays, agencyLeadKilometers);

            if (plan is not null)
            {
                plans.Add(plan);
            }
        }

        return plans;
    }

    /// <summary>
    /// Resolves one pair. Null when neither clock can run (no interval anywhere).
    /// <paramref name="carId"/> is passed in because the new-car form has no car yet.
    /// </summary>
    public static CarExpensePlan? Resolve(
        int carId,
        CarExpenseTypeRule rule,
        CarExpenseBaseline? baseline,
        CarExpenseScheduleValues? custom,
        int agencyLeadDays,
        int agencyLeadKilometers)
    {
        var afterMonths = custom?.AfterMonths ?? rule.AfterMonths;
        var afterKilometers = custom?.AfterKilometers ?? rule.AfterKilometers;

        if (afterMonths is not > 0 && afterKilometers is not > 0)
        {
            return null;
        }

        // Declared and invoiced baselines are both real: take whichever is further along.
        var lastDoneOn = Later(baseline?.LastExpenseOn, custom?.LastDoneOn);
        var lastDoneMileage = Higher(baseline?.LastExpenseMileage, custom?.LastDoneMileage);

        return new CarExpensePlan(
            carId,
            rule.ExpenseTypeId,
            afterMonths,
            afterKilometers,
            custom?.LeadDays ?? agencyLeadDays,
            custom?.LeadKilometers ?? agencyLeadKilometers,
            lastDoneOn,
            lastDoneMileage,
            IsCustom: custom is not null
                && (custom.AfterMonths.HasValue || custom.AfterKilometers.HasValue));
    }

    private static DateTime? Later(DateTime? left, DateTime? right) =>
        left is null ? right : right is null ? left : left > right ? left : right;

    private static int? Higher(int? left, int? right) =>
        left is null ? right : right is null ? left : Math.Max(left.Value, right.Value);
}
