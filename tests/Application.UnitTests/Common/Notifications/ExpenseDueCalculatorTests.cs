using FluentAssertions;
using NUnit.Framework;
using RemSolution.Application.Common.Notifications;

namespace RemSolution.Application.UnitTests.Common.Notifications;

// The rule behind the maintenance/papers alerts: a recurring expense type is due
// again a number of months, or a number of kilometres, after it was last booked
// for a car. See ExpenseDueCalculator.
public class ExpenseDueCalculatorTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    private static ExpenseDue? Evaluate(
        int? afterMonths = null,
        int? afterKilometers = null,
        DateTime? lastExpenseOn = null,
        int? lastExpenseMileage = null,
        int? currentMileage = null,
        int leadDays = 14,
        int leadKilometers = 1000) =>
        ExpenseDueCalculator.Evaluate(
            afterMonths, afterKilometers, lastExpenseOn, lastExpenseMileage,
            currentMileage, Now, leadDays, leadKilometers);

    // ---- Nothing to say ----------------------------------------------------

    [Test]
    public void SaysNothingWithoutABaseline()
    {
        // A car that has never had this cost booked has nothing to count from —
        // deliberately silent rather than alarming (see the calculator's remarks).
        Evaluate(afterMonths: 12, lastExpenseOn: null).Should().BeNull();
    }

    [Test]
    public void SaysNothingWhenTheTypeHasNoInterval()
    {
        Evaluate(lastExpenseOn: Now.AddYears(-5), lastExpenseMileage: 10_000, currentMileage: 90_000)
            .Should().BeNull();
    }

    [Test]
    public void SaysNothingWhileTheDueDateIsBeyondTheWarningWindow()
    {
        // Due in 12 months, a fortnight of warning: silence for another 11½.
        Evaluate(afterMonths: 12, lastExpenseOn: Now).Should().BeNull();
    }

    // ---- The date clock ----------------------------------------------------

    [Test]
    public void WarnsInsideTheWindowBeforeTheDueDate()
    {
        // Booked 12 months ago all but 10 days, so due in 10 — inside a 14-day lead.
        var due = Evaluate(afterMonths: 12, lastExpenseOn: Now.AddYears(-1).AddDays(10));

        due.Should().NotBeNull();
        due!.Basis.Should().Be(ExpenseDueBasis.Date);
        due.IsOverdue.Should().BeFalse();
        due.Days.Should().Be(10);
        due.DueOn.Should().Be(Now.AddYears(-1).AddDays(10).AddMonths(12));
        due.MessageKey.Should().Be(NotificationMessages.CarExpenseDueByDate);
    }

    [Test]
    public void ReportsAnOverdueDateWithDaysSinceItPassed()
    {
        var due = Evaluate(afterMonths: 12, lastExpenseOn: Now.AddYears(-1).AddDays(-5));

        due.Should().NotBeNull();
        due!.IsOverdue.Should().BeTrue();
        // Days is a magnitude, never negative: the wording says "ago".
        due.Days.Should().Be(5);
        due.MessageKey.Should().Be(NotificationMessages.CarExpenseOverdueByDate);
    }

    [Test]
    public void CountsMonthsByCalendar()
    {
        // 31 January + 1 month is 28 February, which is what the paperwork says —
        // a fixed 30-day count would put it on 2 March.
        var due = Evaluate(
            afterMonths: 1,
            lastExpenseOn: new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            leadDays: 365);

        due!.DueOn.Should().Be(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void IgnoresTheTimeOfDay()
    {
        // The same due date evaluated from any hour reports the same day count, so
        // an hourly sweep does not contradict itself through the day.
        var lastExpenseOn = Now.AddYears(-1).AddDays(10).AddHours(-11);

        var due = Evaluate(afterMonths: 12, lastExpenseOn: lastExpenseOn);

        due!.Days.Should().Be(10);
    }

    // ---- The distance clock ------------------------------------------------

    [Test]
    public void WarnsInsideTheWindowBeforeTheDueOdometer()
    {
        // Serviced at 90 000, due every 10 000, now at 99 200 — 800 km to go.
        var due = Evaluate(
            afterKilometers: 10_000, lastExpenseMileage: 90_000, currentMileage: 99_200);

        due.Should().NotBeNull();
        due!.Basis.Should().Be(ExpenseDueBasis.Distance);
        due.IsOverdue.Should().BeFalse();
        due.DueAtKilometers.Should().Be(100_000);
        due.Kilometers.Should().Be(800);
        due.MessageKey.Should().Be(NotificationMessages.CarExpenseDueByDistance);
    }

    [Test]
    public void ReportsAnOverrunAsAMagnitude()
    {
        var due = Evaluate(
            afterKilometers: 10_000, lastExpenseMileage: 90_000, currentMileage: 100_450);

        due!.IsOverdue.Should().BeTrue();
        due.Kilometers.Should().Be(450);
        due.MessageKey.Should().Be(NotificationMessages.CarExpenseOverdueByDistance);
    }

    [Test]
    public void SaysNothingAboutDistanceWithoutAReadingWhenTheWorkWasDone()
    {
        // Expenses recorded before the odometer field existed land here: the
        // distance clock has no answer, and inventing zero would report every one
        // of them as wildly overdue.
        Evaluate(afterKilometers: 10_000, lastExpenseMileage: null, currentMileage: 150_000)
            .Should().BeNull();
    }

    [Test]
    public void SaysNothingAboutDistanceWhenTheCarsOdometerIsUnknown()
    {
        Evaluate(afterKilometers: 10_000, lastExpenseMileage: 90_000, currentMileage: null)
            .Should().BeNull();
    }

    // ---- Both clocks running ----------------------------------------------

    [Test]
    public void PrefersAnOverdueClockOverAnApproachingOne()
    {
        // Due in 10 days by date, but 450 km past the odometer threshold.
        var due = Evaluate(
            afterMonths: 12,
            afterKilometers: 10_000,
            lastExpenseOn: Now.AddYears(-1).AddDays(10),
            lastExpenseMileage: 90_000,
            currentMileage: 100_450);

        due!.Basis.Should().Be(ExpenseDueBasis.Distance);
        due.IsOverdue.Should().BeTrue();
    }

    [Test]
    public void PrefersTheNearerClockWhenBothAreMerelyApproaching()
    {
        // 12 of 14 days left (86% of the window) against 200 of 1 000 km (20%):
        // the odometer is the more urgent of the two.
        var due = Evaluate(
            afterMonths: 12,
            afterKilometers: 10_000,
            lastExpenseOn: Now.AddYears(-1).AddDays(12),
            lastExpenseMileage: 90_000,
            currentMileage: 99_800);

        due!.Basis.Should().Be(ExpenseDueBasis.Distance);
        due.IsOverdue.Should().BeFalse();
    }

    [Test]
    public void PrefersTheFurtherOverrunWhenBothClocksHavePassed()
    {
        // 30 days over a 14-day window against 100 km over a 1 000 km one.
        var due = Evaluate(
            afterMonths: 12,
            afterKilometers: 10_000,
            lastExpenseOn: Now.AddYears(-1).AddDays(-30),
            lastExpenseMileage: 90_000,
            currentMileage: 100_100);

        due!.Basis.Should().Be(ExpenseDueBasis.Date);
        due.IsOverdue.Should().BeTrue();
        due.Days.Should().Be(30);
    }

    [Test]
    public void ReportsOneClockWhenTheOtherIsSilent()
    {
        // Nothing due by odometer, overdue by date: the date is still reported.
        var due = Evaluate(
            afterMonths: 12,
            afterKilometers: 10_000,
            lastExpenseOn: Now.AddYears(-1).AddDays(-5),
            lastExpenseMileage: 90_000,
            currentMileage: 91_000);

        due!.Basis.Should().Be(ExpenseDueBasis.Date);
    }

    // ---- Lead times as the agency set them --------------------------------

    [Test]
    public void HonoursAZeroLeadTimeAsWarnOnlyOnceItIsDue()
    {
        // Zero warning window: due tomorrow is still silence.
        Evaluate(afterMonths: 12, lastExpenseOn: Now.AddYears(-1).AddDays(1), leadDays: 0)
            .Should().BeNull();

        // Due today speaks up.
        Evaluate(afterMonths: 12, lastExpenseOn: Now.AddYears(-1), leadDays: 0)
            .Should().NotBeNull();
    }

    [Test]
    public void HonoursALongerLeadTime()
    {
        var due = Evaluate(afterMonths: 12, lastExpenseOn: Now.AddYears(-1).AddDays(40), leadDays: 60);

        due.Should().NotBeNull();
        due!.Days.Should().Be(40);
    }
}
