using FluentAssertions;
using NUnit.Framework;
using RemSolution.Application.Common.Notifications;

namespace RemSolution.Application.UnitTests.Common.Notifications;

// How a car's own figures fold together with the type's rule and the agency's
// warning windows. See CarExpenseSchedules.
public class CarExpenseSchedulesTests
{
    private const int TypeId = 7;
    private const int CarId = 42;
    private const int AgencyLeadDays = 14;
    private const int AgencyLeadKilometers = 1000;

    // "Vidange": every 10 000 km or every 12 months across the whole fleet.
    private static readonly CarExpenseTypeRule Vidange = new(TypeId, AfterMonths: 12, AfterKilometers: 10_000);

    private static CarExpensePlan? Resolve(
        CarExpenseTypeRule? rule = null,
        CarExpenseBaseline? baseline = null,
        CarExpenseScheduleValues? custom = null) =>
        CarExpenseSchedules.Resolve(
            CarId, rule ?? Vidange, baseline, custom, AgencyLeadDays, AgencyLeadKilometers);

    private static CarExpenseScheduleValues Schedule(
        int? afterMonths = null,
        int? afterKilometers = null,
        int? leadDays = null,
        int? leadKilometers = null,
        DateTime? lastDoneOn = null,
        int? lastDoneMileage = null) =>
        new(CarId, TypeId, afterMonths, afterKilometers, leadDays, leadKilometers,
            lastDoneOn, lastDoneMileage);

    // ---- Falling back -------------------------------------------------------

    [Test]
    public void FollowsTheTypeWhenTheCarSaysNothing()
    {
        var plan = Resolve(baseline: new CarExpenseBaseline(CarId, TypeId, null, 80_000));

        plan!.AfterKilometers.Should().Be(10_000);
        plan.AfterMonths.Should().Be(12);
        plan.LeadDays.Should().Be(AgencyLeadDays);
        plan.LeadKilometers.Should().Be(AgencyLeadKilometers);
        plan.IsCustom.Should().BeFalse();
    }

    [Test]
    public void TakesTheCarsIntervalOverTheTypes()
    {
        // The same oil change is every 8 000 km on this van.
        var plan = Resolve(custom: Schedule(afterKilometers: 8_000));

        plan!.AfterKilometers.Should().Be(8_000);
        plan.IsCustom.Should().BeTrue();
    }

    [Test]
    public void OverridingOneClockLeavesTheOtherOnTheTypes()
    {
        var plan = Resolve(custom: Schedule(afterKilometers: 8_000));

        plan!.AfterMonths.Should().Be(12);
    }

    [Test]
    public void TakesTheCarsWarningWindowOverTheAgencys()
    {
        var plan = Resolve(custom: Schedule(afterKilometers: 8_000, leadKilometers: 250, leadDays: 3));

        plan!.LeadKilometers.Should().Be(250);
        plan.LeadDays.Should().Be(3);
    }

    [Test]
    public void ReadsAZeroWarningWindowAsAChoiceRatherThanAnOmission()
    {
        // A zero lead is a real answer, not a missing one.
        var plan = Resolve(custom: Schedule(afterKilometers: 8_000, leadDays: 0, leadKilometers: 0));

        plan!.LeadDays.Should().Be(0);
        plan.LeadKilometers.Should().Be(0);
    }

    [Test]
    public void SuppliesTheIntervalForATypeThatStatesNone()
    {
        // A notifiable type with no figures is legal: cars supply their own.
        var plan = Resolve(
            rule: new CarExpenseTypeRule(TypeId, null, null),
            custom: Schedule(afterKilometers: 8_000, lastDoneMileage: 80_000));

        plan!.AfterKilometers.Should().Be(8_000);
    }

    [Test]
    public void SaysNothingWhenNoIntervalExistsAnywhere()
    {
        Resolve(
            rule: new CarExpenseTypeRule(TypeId, null, null),
            baseline: new CarExpenseBaseline(CarId, TypeId, DateTime.UtcNow, 80_000))
            .Should().BeNull();
    }

    [Test]
    public void SaysNothingWhenTheOnlyIntervalIsZero()
    {
        Resolve(rule: new CarExpenseTypeRule(TypeId, 0, 0), custom: Schedule(lastDoneMileage: 1))
            .Should().BeNull();
    }

    // ---- The baseline -------------------------------------------------------

    [Test]
    public void CountsFromTheDeclaredBaselineBeforeAnyExpenseExists()
    {
        // Work done before the agency used the software starts the schedule.
        var plan = Resolve(custom: Schedule(
            lastDoneMileage: 80_000, lastDoneOn: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        plan!.LastDoneMileage.Should().Be(80_000);
        plan.LastDoneOn.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void PrefersTheInvoicedBaselineOnceItOvertakesTheDeclaredOne()
    {
        var declared = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoiced = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var plan = Resolve(
            baseline: new CarExpenseBaseline(CarId, TypeId, invoiced, 88_000),
            custom: Schedule(lastDoneOn: declared, lastDoneMileage: 80_000));

        plan!.LastDoneOn.Should().Be(invoiced);
        plan.LastDoneMileage.Should().Be(88_000);
    }

    [Test]
    public void KeepsTheDeclaredBaselineWhenTheExpenseIsTheOlderOne()
    {
        // Booking a forgotten old invoice must not drag the schedule backwards.
        var declared = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoiced = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var plan = Resolve(
            baseline: new CarExpenseBaseline(CarId, TypeId, invoiced, 70_000),
            custom: Schedule(lastDoneOn: declared, lastDoneMileage: 80_000));

        plan!.LastDoneOn.Should().Be(declared);
        plan.LastDoneMileage.Should().Be(80_000);
    }

    [Test]
    public void DoesNotCallACarCustomForDeclaringOnlyWhenTheWorkWasDone()
    {
        // IsCustom is about the interval; a declared baseline is not an override.
        var plan = Resolve(custom: Schedule(lastDoneMileage: 80_000));

        plan!.IsCustom.Should().BeFalse();
    }

    // ---- Pairing ------------------------------------------------------------

    [Test]
    public void PlansPairsFromBothTheExpenseHistoryAndTheSchedules()
    {
        var plans = CarExpenseSchedules.Plan(
            new[] { Vidange },
            new[] { new CarExpenseBaseline(1, TypeId, null, 50_000) },
            new[] { Schedule(afterKilometers: 8_000, lastDoneMileage: 80_000) with { CarId = 2 } },
            AgencyLeadDays,
            AgencyLeadKilometers);

        plans.Select(p => p.CarId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Test]
    public void PlansOnePairPerCarAndTypeWhenBothSourcesDescribeIt()
    {
        var plans = CarExpenseSchedules.Plan(
            new[] { Vidange },
            new[] { new CarExpenseBaseline(CarId, TypeId, null, 50_000) },
            new[] { Schedule(afterKilometers: 8_000) },
            AgencyLeadDays,
            AgencyLeadKilometers);

        plans.Should().ContainSingle()
            .Which.AfterKilometers.Should().Be(8_000);
    }

    [Test]
    public void IgnoresSchedulesForTypesThatNoLongerNotify()
    {
        // A row left behind by a switched-off type must not resurrect it.
        var plans = CarExpenseSchedules.Plan(
            Array.Empty<CarExpenseTypeRule>(),
            Array.Empty<CarExpenseBaseline>(),
            new[] { Schedule(afterKilometers: 8_000, lastDoneMileage: 80_000) },
            AgencyLeadDays,
            AgencyLeadKilometers);

        plans.Should().BeEmpty();
    }
}
