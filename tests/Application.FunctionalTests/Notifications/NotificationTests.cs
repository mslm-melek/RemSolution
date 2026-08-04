using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Features.Notification.Commands.MarkNotificationsReadCommand;
using RemSolution.Application.Features.Notification.Commands.SendClientLateNoticeCommand;
using RemSolution.Application.Features.Notification.Queries.GetMyNotificationsQuery;
using RemSolution.Application.Features.Notification.Queries.GetMyUnreadNotificationCountQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Identity;
using RemSolution.Infrastructure.Jobs;

namespace RemSolution.Application.FunctionalTests.Notifications;

using static Testing;

// The notification module end to end: what the sweep finds, who it tells, and the
// one notice a person sends by hand.
public class NotificationTests : BaseTestFixture
{
    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    /// <summary>
    /// Links a test user to the agency. The shared harness creates users without
    /// one (the tenant travels as a claim in a request), but the sweep has no
    /// request to read: it asks the Identity store who works for the agency, so
    /// the column has to be set for a user to be a recipient at all.
    /// </summary>
    private static async Task JoinAgencyAsync(string userId, int agencyId) =>
        await UsingScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);

            user!.AgencyId = agencyId;
            await users.UpdateAsync(user);

            return true;
        });

    private static async Task RunSweepAsync() =>
        await UsingScopeAsync(async services =>
        {
            await services.GetRequiredService<NotificationSweepJob>().RunAsync();
            return true;
        });

    /// <summary>Every notification row in the database, tenant filter bypassed.</summary>
    private static Task<List<Notification>> AllNotificationsAsync() =>
        AllIgnoringFiltersAsync<Notification>();

    private static async Task<int> CarAsync(string matricule, int? mileage = null)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active, Mileage = mileage };
        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> ClientAsync(string? email = null)
    {
        var client = new Client { FirstName = "Nota", LastName = "Client", Email = email };
        await AddAsync(client);
        return client.Id;
    }

    private static async Task<int> RentingAsync(
        int carId, int clientId, DateTime start, DateTime end, RentingState state)
    {
        var renting = new Renting
        {
            CarId = carId,
            ClientId = clientId,
            StartDate = start,
            EndDate = end,
            RentingState = state,
            Price = Money.Of(200m, "TND")
        };
        await AddAsync(renting);
        return renting.Id;
    }

    private static async Task SetNotificationSettingsAsync(int agencyId, Action<AgencySettings> mutate) =>
        await UsingScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ApplicationDbContext>();

            var settings = await context.AgencySettings.FirstAsync(s => s.AgencyId == agencyId);
            mutate(settings);
            await context.SaveChangesAsync(CancellationToken.None);

            // The provider caches per agency, so a test that edits settings has to
            // drop the snapshot the sweep would otherwise read.
            services.GetRequiredService<RemSolution.Application.Common.Settings.IAgencySettingsProvider>()
                .Invalidate(agencyId);

            return true;
        });

    // ---------------------------------------------------------------------
    // Late hires — who hears about them
    // ---------------------------------------------------------------------

    [Test]
    public async Task ALateHireIsReportedToStaffWhoCanSeeHires()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.RentingRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-1");
        var clientId = await ClientAsync();
        var rentingId = await RentingAsync(
            carId, clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-3),
            RentingState.InProgress);

        await RunSweepAsync();

        var inbox = await SendAsync(new GetMyNotificationsQuery());

        inbox.Items.Should().HaveCount(1);

        var notification = inbox.Items.First();
        notification.Kind.Should().Be(NotificationKind.RentingOverdue);
        notification.MessageKey.Should().Be(NotificationMessages.RentingOverdue);
        notification.SubjectType.Should().Be(NotificationSubject.Renting);
        notification.SubjectId.Should().Be(rentingId);
        notification.Link.Should().Be($"/renting/{rentingId}");
        notification.IsRead.Should().BeFalse();
        // The wording is assembled by the client from these; three days late.
        notification.Args["days"].Should().Be("3");
        notification.Args.Should().ContainKey("car");
        notification.Args.Should().ContainKey("endDate");
    }

    [Test]
    public async Task ALateHireIsNotReportedToStaffWhoCannotSeeHires()
    {
        // The permission behind the alert IS the access rule: an inbox must not
        // leak a booking to somebody who could not open it.
        var userId = await RunAsAgencyStaffAsync(Permissions.ClientRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-2");
        var clientId = await ClientAsync();
        await RentingAsync(
            carId, clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-3),
            RentingState.InProgress);

        await RunSweepAsync();

        var inbox = await SendAsync(new GetMyNotificationsQuery());

        inbox.Items.Should().BeEmpty();
    }

    [Test]
    public async Task AnAgencyAdministratorHearsAboutItWithoutAnyGrant()
    {
        // Administrators hold every permission implicitly, the same rule the
        // authorization policies apply.
        var userId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-3");
        var clientId = await ClientAsync();
        await RentingAsync(
            carId, clientId,
            DateTime.UtcNow.AddDays(-4), DateTime.UtcNow.AddDays(-1),
            RentingState.InProgress);

        await RunSweepAsync();

        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().HaveCount(1);
    }

    [Test]
    public async Task AHireThatCameBackIsNotReported()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.RentingRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-4");
        var clientId = await ClientAsync();
        await RentingAsync(
            carId, clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-3),
            RentingState.Done);

        await RunSweepAsync();

        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().BeEmpty();
    }

    [Test]
    public async Task RunningTheSweepAgainSaysNothingTwice()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.RentingRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-5");
        var clientId = await ClientAsync();
        await RentingAsync(
            carId, clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-3),
            RentingState.InProgress);

        // The job runs hourly; the same finding must not fill the inbox.
        await RunSweepAsync();
        await RunSweepAsync();
        await RunSweepAsync();

        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().HaveCount(1);
    }

    // ---------------------------------------------------------------------
    // Maintenance and papers, as recurring expense types
    // ---------------------------------------------------------------------

    [Test]
    public async Task ARecurringExpenseComingDueIsReportedFromTheLastOneBooked()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ExpenseRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-6");

        var type = new ExpenseType
        {
            Name = "Insurance",
            IsActive = true,
            WithNotif = true,
            AfterMonth = 12
        };
        await AddAsync(type);

        // Booked a year ago all but ten days, so it is due inside the agency's
        // fortnight of warning.
        await AddAsync(new Expense
        {
            CarId = carId,
            ExpenseTypeId = type.Id,
            ExpenseDate = DateTime.UtcNow.AddYears(-1).AddDays(10),
            ExpenseAmount = Money.Of(600m, "TND")
        });

        await RunSweepAsync();

        var inbox = await SendAsync(new GetMyNotificationsQuery());

        inbox.Items.Should().HaveCount(1);

        var notification = inbox.Items.First();
        notification.Kind.Should().Be(NotificationKind.CarExpenseDue);
        notification.MessageKey.Should().Be(NotificationMessages.CarExpenseDueByDate);
        notification.SubjectType.Should().Be(NotificationSubject.Car);
        notification.SubjectId.Should().Be(carId);
        notification.Args["type"].Should().Be("Insurance");
    }

    [Test]
    public async Task ARecurringExpenseWithNothingBookedYetIsNotReported()
    {
        // No baseline means the schedule cannot be computed, and warning about
        // every car on the day the feature is switched on would be noise nobody
        // reads afterwards (see ExpenseDueCalculator).
        var userId = await RunAsAgencyStaffAsync(Permissions.ExpenseRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        await CarAsync("NT-7");
        await AddAsync(new ExpenseType
        {
            Name = "Technical inspection", IsActive = true, WithNotif = true, AfterMonth = 12
        });

        await RunSweepAsync();

        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().BeEmpty();
    }

    [Test]
    public async Task AnExpenseTypeNotFlaggedForNotificationIsSilent()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ExpenseRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-8");

        var type = new ExpenseType { Name = "Fuel", IsActive = true, WithNotif = false, AfterMonth = 1 };
        await AddAsync(type);

        await AddAsync(new Expense
        {
            CarId = carId,
            ExpenseTypeId = type.Id,
            ExpenseDate = DateTime.UtcNow.AddYears(-2),
            ExpenseAmount = Money.Of(80m, "TND")
        });

        await RunSweepAsync();

        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().BeEmpty();
    }

    [Test]
    public async Task ADistanceIntervalIsMeasuredFromTheOdometerWhenTheWorkWasDone()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ExpenseRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        // Serviced at 90 000, due every 10 000, now at 100 400 — overdue by 400.
        var carId = await CarAsync("NT-9", mileage: 100_400);

        var type = new ExpenseType
        {
            Name = "Servicing", IsActive = true, WithNotif = true, AfterKilometer = 10_000
        };
        await AddAsync(type);

        await AddAsync(new Expense
        {
            CarId = carId,
            ExpenseTypeId = type.Id,
            ExpenseDate = DateTime.UtcNow.AddMonths(-3),
            Mileage = 90_000,
            ExpenseAmount = Money.Of(150m, "TND")
        });

        await RunSweepAsync();

        var notification = (await SendAsync(new GetMyNotificationsQuery())).Items.Single();

        notification.MessageKey.Should().Be(NotificationMessages.CarExpenseOverdueByDistance);
        notification.Args["dueKm"].Should().Be("100000");
        notification.Args["km"].Should().Be("400");
    }

    // ---------------------------------------------------------------------
    // Upcoming pickups
    // ---------------------------------------------------------------------

    [Test]
    public async Task AConfirmedHoldStartingSoonIsReported()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ReservationRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-10");
        var clientId = await ClientAsync();

        var reservation = Reservation.Create(
            carId,
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(5),
            Money.Of(300m, "TND"),
            DateTime.UtcNow.AddDays(1),
            clientId);
        reservation.Confirm();
        await AddAsync(reservation);

        await RunSweepAsync();

        var notification = (await SendAsync(new GetMyNotificationsQuery())).Items.Single();

        notification.Kind.Should().Be(NotificationKind.ReservationUpcoming);
        notification.SubjectType.Should().Be(NotificationSubject.Reservation);
        notification.SubjectId.Should().Be(reservation.Id);
    }

    [Test]
    public async Task AHoldStillAwaitingConfirmationIsNotAPickupToPrepareFor()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ReservationRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var carId = await CarAsync("NT-11");
        var clientId = await ClientAsync();

        await AddAsync(Reservation.Create(
            carId,
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(5),
            Money.Of(300m, "TND"),
            DateTime.UtcNow.AddDays(1),
            clientId));

        await RunSweepAsync();

        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // Reading and clearing
    // ---------------------------------------------------------------------

    [Test]
    public async Task MarkingReadClearsTheBadge()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.RentingRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var clientId = await ClientAsync();
        await RentingAsync(
            await CarAsync("NT-12"), clientId,
            DateTime.UtcNow.AddDays(-9), DateTime.UtcNow.AddDays(-2), RentingState.InProgress);
        await RentingAsync(
            await CarAsync("NT-13"), clientId,
            DateTime.UtcNow.AddDays(-8), DateTime.UtcNow.AddDays(-1), RentingState.InProgress);

        await RunSweepAsync();

        (await SendAsync(new GetMyUnreadNotificationCountQuery())).Should().Be(2);

        var first = (await SendAsync(new GetMyNotificationsQuery())).Items.First();
        (await SendAsync(new MarkNotificationsReadCommand(new[] { first.Id }))).Should().Be(1);

        (await SendAsync(new GetMyUnreadNotificationCountQuery())).Should().Be(1);

        // No ids means "every unread one of mine".
        (await SendAsync(new MarkNotificationsReadCommand())).Should().Be(1);
        (await SendAsync(new GetMyUnreadNotificationCountQuery())).Should().Be(0);

        // Idempotent: a second pass has nothing left to mark.
        (await SendAsync(new MarkNotificationsReadCommand())).Should().Be(0);
    }

    [Test]
    public async Task TheUnreadFilterShowsOnlyWhatIsUnread()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.RentingRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        await RentingAsync(
            await CarAsync("NT-14"), await ClientAsync(),
            DateTime.UtcNow.AddDays(-9), DateTime.UtcNow.AddDays(-2), RentingState.InProgress);

        await RunSweepAsync();
        await SendAsync(new MarkNotificationsReadCommand());

        (await SendAsync(new GetMyNotificationsQuery(OnlyUnread: true))).Items.Should().BeEmpty();
        (await SendAsync(new GetMyNotificationsQuery())).Items.Should().HaveCount(1);
    }

    [Test]
    public async Task OneStaffMemberCannotClearAnothersInbox()
    {
        var agencyId = await AddTestAgencyAsync();

        var colleague = await RunAsUserAsync(
            "colleague@local", "Colleague1234!", new[] { Roles.AgencyStaff });
        await AddAsync(new UserPermission { UserId = colleague, Permission = Permissions.RentingRead });
        await JoinAgencyAsync(colleague, agencyId);

        var mine = await RunAsAgencyStaffAsync(Permissions.RentingRead);
        await JoinAgencyAsync(mine, agencyId);

        await RentingAsync(
            await CarAsync("NT-15"), await ClientAsync(),
            DateTime.UtcNow.AddDays(-9), DateTime.UtcNow.AddDays(-2), RentingState.InProgress);

        await RunSweepAsync();

        // Both were told; clearing one inbox leaves the other alone, because the
        // command's recipient clause is what any id is matched against.
        var mineIds = (await SendAsync(new GetMyNotificationsQuery())).Items.Select(n => n.Id).ToList();
        (await SendAsync(new MarkNotificationsReadCommand(mineIds))).Should().Be(1);

        var rows = await AllNotificationsAsync();
        rows.Where(r => r.RecipientUserId == colleague).Should().OnlyContain(r => r.ReadAt == null);
        rows.Where(r => r.RecipientUserId == mine).Should().OnlyContain(r => r.ReadAt != null);
    }

    // ---------------------------------------------------------------------
    // Client reminders
    // ---------------------------------------------------------------------

    [Test]
    public async Task ClientRemindersAreNotInAnybodysInbox()
    {
        var userId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        await SetNotificationSettingsAsync(agencyId, settings =>
        {
            settings.NotifyClientsByEmail = true;
            settings.ClientReminderDaysBeforeStart = 3;
        });

        var carId = await CarAsync("NT-16");
        var clientId = await ClientAsync("renter@example.com");
        var rentingId = await RentingAsync(
            carId, clientId,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(6), RentingState.NotYet);

        await RunSweepAsync();

        // The mail is recorded, so the sweep will not send it again…
        var reminder = (await AllNotificationsAsync())
            .Should().ContainSingle(r => r.Kind == NotificationKind.RentingStartingSoon).Subject;

        reminder.RecipientEmail.Should().Be("renter@example.com");
        reminder.ClientId.Should().Be(clientId);
        reminder.SubjectId.Should().Be(rentingId);
        reminder.EmailSentAt.Should().NotBeNull();
        // …but it is a record of a letter, not an inbox entry.
        reminder.RecipientUserId.Should().BeNull();

        (await SendAsync(new GetMyNotificationsQuery())).Items
            .Should().NotContain(n => n.Kind == NotificationKind.RentingStartingSoon);
    }

    [Test]
    public async Task ClientsAreNotWrittenToWhileTheAgencyHasItSwitchedOff()
    {
        // Off by default: an agency opts in to mailing its customers.
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        await RentingAsync(
            await CarAsync("NT-17"), await ClientAsync("quiet@example.com"),
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(6), RentingState.NotYet);

        await RunSweepAsync();

        (await AllNotificationsAsync())
            .Should().NotContain(r => r.Kind == NotificationKind.RentingStartingSoon);
    }

    [Test]
    public async Task AZeroLeadTimeSwitchesThatOneReminderOff()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        await SetNotificationSettingsAsync(agencyId, settings =>
        {
            settings.NotifyClientsByEmail = true;
            settings.ClientReminderDaysBeforeStart = 0;
            settings.ClientReminderDaysBeforeEnd = 3;
        });

        var clientId = await ClientAsync("both@example.com");

        // Starting soon (reminder off) and ending soon (reminder on).
        await RentingAsync(
            await CarAsync("NT-18"), clientId,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(6), RentingState.NotYet);
        await RentingAsync(
            await CarAsync("NT-19"), clientId,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(1), RentingState.InProgress);

        await RunSweepAsync();

        var rows = await AllNotificationsAsync();

        rows.Should().NotContain(r => r.Kind == NotificationKind.RentingStartingSoon);
        rows.Should().ContainSingle(r => r.Kind == NotificationKind.RentingEndingSoon);
    }

    // ---------------------------------------------------------------------
    // The notice a person sends by hand
    // ---------------------------------------------------------------------

    [Test]
    public async Task TheLateNoticeWritesToTheClientAboutTheirOverdueHire()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.NotificationSend);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var clientId = await ClientAsync("late@example.com");
        var rentingId = await RentingAsync(
            await CarAsync("NT-20"), clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-4), RentingState.InProgress);

        var result = await SendAsync(new SendClientLateNoticeCommand(clientId));

        result.Outcome.Should().Be(ClientNotificationOutcome.Sent);
        result.RentingId.Should().Be(rentingId);

        var row = (await AllNotificationsAsync())
            .Should().ContainSingle(r => r.Kind == NotificationKind.RentingLateNotice).Subject;

        row.RecipientEmail.Should().Be("late@example.com");
        row.SubjectId.Should().Be(rentingId);
        row.EmailSentAt.Should().NotBeNull();
        // Set only for a notice a person triggered — how the hand-sent ones are
        // told apart from the sweep's.
        row.SentByUserId.Should().Be(userId);
    }

    [Test]
    public async Task TheLateNoticeIsNotSentTwiceInADay()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.NotificationSend);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var clientId = await ClientAsync("twice@example.com");
        await RentingAsync(
            await CarAsync("NT-21"), clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-4), RentingState.InProgress);

        (await SendAsync(new SendClientLateNoticeCommand(clientId))).Outcome
            .Should().Be(ClientNotificationOutcome.Sent);

        // A double-clicked button writes once.
        (await SendAsync(new SendClientLateNoticeCommand(clientId))).Outcome
            .Should().Be(ClientNotificationOutcome.AlreadySent);

        (await CountAsync<Notification>()).Should().Be(1);
    }

    [Test]
    public async Task TheLateNoticeSaysSoWhenThereIsNothingOverdue()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.NotificationSend);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var clientId = await ClientAsync("ontime@example.com");
        await RentingAsync(
            await CarAsync("NT-22"), clientId,
            DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(3), RentingState.InProgress);

        var result = await SendAsync(new SendClientLateNoticeCommand(clientId));

        result.Outcome.Should().Be(ClientNotificationOutcome.NothingToSend);
        result.RentingId.Should().BeNull();
        (await CountAsync<Notification>()).Should().Be(0);
    }

    [Test]
    public async Task TheLateNoticeSaysSoWhenTheClientHasNoAddress()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.NotificationSend);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var clientId = await ClientAsync(email: null);
        await RentingAsync(
            await CarAsync("NT-23"), clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-4), RentingState.InProgress);

        (await SendAsync(new SendClientLateNoticeCommand(clientId))).Outcome
            .Should().Be(ClientNotificationOutcome.NoEmail);

        // Nothing happened, so nothing is recorded as having happened.
        (await CountAsync<Notification>()).Should().Be(0);
    }

    [Test]
    public async Task TheLateNoticeGoesOutEvenWhileAutomaticClientMailIsOff()
    {
        // A staff member pressing the button has made the decision that setting
        // exists to defer (see SendClientLateNoticeCommand).
        var userId = await RunAsAgencyStaffAsync(Permissions.NotificationSend);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        await SetNotificationSettingsAsync(agencyId, settings => settings.NotifyClientsByEmail = false);

        var clientId = await ClientAsync("deliberate@example.com");
        await RentingAsync(
            await CarAsync("NT-24"), clientId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-4), RentingState.InProgress);

        (await SendAsync(new SendClientLateNoticeCommand(clientId))).Outcome
            .Should().Be(ClientNotificationOutcome.Sent);
    }

    [Test]
    public async Task TheLateNoticeNeedsThePermissionToSendIt()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ClientRead);
        var agencyId = await AddTestAgencyAsync();
        await JoinAgencyAsync(userId, agencyId);

        var clientId = await ClientAsync("forbidden@example.com");

        await FluentActions.Invoking(() => SendAsync(new SendClientLateNoticeCommand(clientId)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }
}
