using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Chat.Commands.MarkChatReadCommand;
using RemSolution.Application.Features.Chat.Commands.SendChatMessageCommand;
using RemSolution.Application.Features.Chat.Queries.GetChatMessagesQuery;
using RemSolution.Application.Features.Chat.Queries.GetChatThreadsQuery;
using RemSolution.Application.Features.Marketplace.Commands.MarkMyChatReadCommand;
using RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatMessagesQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatThreadsQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Chat;

using static Testing;

public class ChatTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 9, 5, 0, 0, 0, DateTimeKind.Utc);

    // An ongoing renting, optionally linked to a marketplace customer account.
    private async Task<int> RentingAsync(
        string matricule,
        RentingState state = RentingState.InProgress,
        string? marketplaceUserId = null)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        var client = new Client
        {
            FirstName = "Chat",
            LastName = "Client",
            MarketplaceUserId = marketplaceUserId
        };
        await AddAsync(client);

        var renting = RentingFixture.Hire(
            car.Id, client.Id, Start, End, state, price: Money.Of(200m, "TND"));
        await AddAsync(renting);

        return renting.Id;
    }

    [Test]
    public async Task AgencyMessageIsStoredAsTheAgencySide()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-1");

        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "  Your car is ready  " });

        var messages = await SendAsync(new GetChatMessagesQuery(ChatSubjectKind.Renting, rentingId));
        var message = messages.Single();
        message.AuthorKind.Should().Be(ChatAuthorKind.Agency);
        message.Body.Should().Be("Your car is ready"); // trimmed
        message.ReadAt.Should().BeNull();
    }

    [Test]
    public async Task AClosedRentingRefusesNewMessagesButKeepsItsHistory()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var ongoing = await RentingAsync("CH-OPEN");
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = ongoing, Body = "Hello" });

        var finished = await RentingAsync("CH-DONE", RentingState.Done);

        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Renting, Id = finished, Body = "Too late"
        })).Should().ThrowAsync<ValidationException>();

        // The open thread is unaffected and still readable.
        (await SendAsync(new GetChatMessagesQuery(ChatSubjectKind.Renting, ongoing))).Should().HaveCount(1);
    }

    [Test]
    public async Task TheAfterIdCursorReturnsOnlyNewMessages()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-2");

        var firstId = await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "One" });
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Two" });

        var incoming = await SendAsync(new GetChatMessagesQuery(ChatSubjectKind.Renting, rentingId, AfterId: firstId));

        incoming.Should().HaveCount(1);
        incoming.Single().Body.Should().Be("Two");
    }

    [Test]
    public async Task ThreadsListOngoingRentingsEvenWithNoMessageYet()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await RentingAsync("CH-SILENT");
        var talking = await RentingAsync("CH-TALK");
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = talking, Body = "Hi" });

        var threads = await SendAsync(new GetChatThreadsQuery());

        threads.TotalCount.Should().Be(2);
        // Most recent conversation first; the silent renting falls to the end.
        threads.Items.First().RentingId.Should().Be(talking);
        threads.Items.First().LastMessagePreview.Should().Be("Hi");
        threads.Items.Last().LastMessagePreview.Should().BeNull();
        threads.Items.Should().OnlyContain(x => x.IsOpen);
    }

    [Test]
    public async Task CustomerRepliesInTheirOwnThreadAndTheAgencySeesItUnread()
    {
        var customerId = await RunAsUserAsync("chatcust@local", "Customer1234!", new[] { Roles.Customer });

        // Set the agency up as staff would, then hand the thread to the customer.
        var adminId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-CUST", marketplaceUserId: customerId);
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Welcome" });

        // Act as the customer: signed in, and with NO tenant â€” exactly what a
        // marketplace account looks like, so the cross-tenant path is exercised.
        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        var mine = await SendAsync(new GetMyChatMessagesQuery(ChatSubjectKind.Renting, rentingId));
        mine.Should().HaveCount(1);

        await SendAsync(new SendCustomerChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Thanks!" });

        // Back on the agency side: the reply is there and counts as unread.
        SetCurrentUser(adminId);
        SetCurrentAgency(agencyId);

        var threads = await SendAsync(new GetChatThreadsQuery());
        var thread = threads.Items.Single(x => x.RentingId == rentingId);
        thread.UnreadCount.Should().Be(1);
        thread.LastMessageAuthorKind.Should().Be(ChatAuthorKind.Client);

        await SendAsync(new MarkChatReadCommand(ChatSubjectKind.Renting, rentingId));

        var afterRead = await SendAsync(new GetChatThreadsQuery());
        afterRead.Items.Single(x => x.RentingId == rentingId).UnreadCount.Should().Be(0);
    }

    [Test]
    public async Task MarkReadOnlyStampsTheOtherSidesMessages()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-READ");
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "From the desk" });

        await SendAsync(new MarkChatReadCommand(ChatSubjectKind.Renting, rentingId));

        var messages = await SendAsync(new GetChatMessagesQuery(ChatSubjectKind.Renting, rentingId));
        // The agency never marks its own message read â€” ReadAt means the
        // recipient saw it.
        messages.Single().ReadAt.Should().BeNull();
    }

    [Test]
    public async Task CustomerMarkReadStampsTheAgencyMessages()
    {
        var customerId = await RunAsUserAsync("chatcust3@local", "Customer1234!", new[] { Roles.Customer });

        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-CREAD", marketplaceUserId: customerId);
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Please confirm" });

        SetCurrentUser(customerId);
        SetCurrentAgency(null);
        await SendAsync(new MarkMyChatReadCommand(ChatSubjectKind.Renting, rentingId));

        var mine = await SendAsync(new GetMyChatMessagesQuery(ChatSubjectKind.Renting, rentingId));
        mine.Single().ReadAt.Should().NotBeNull();
    }

    [Test]
    public async Task ACustomerCannotReadOrPostInSomeoneElsesThread()
    {
        var ownerId = await RunAsUserAsync("chatowner@local", "Customer1234!", new[] { Roles.Customer });

        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-PRIVATE", marketplaceUserId: ownerId);
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Private" });

        // A different customer account, with no tenant of its own.
        await RunAsUserAsync("chatstranger@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        (await SendAsync(new GetMyChatMessagesQuery(ChatSubjectKind.Renting, rentingId))).Should().BeEmpty();
        (await SendAsync(new GetMyChatThreadsQuery())).Should().BeEmpty();

        await FluentActions.Invoking(() => SendAsync(new SendCustomerChatMessageCommand
        {
            Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Let me in"
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task AnotherAgencyCannotSeeOrPostInTheThread()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-TENANT");
        await SendAsync(new SendChatMessageCommand { Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Ours" });

        await AddTestAgencyAsync(); // second tenant

        (await SendAsync(new GetChatMessagesQuery(ChatSubjectKind.Renting, rentingId))).Should().BeEmpty();
        (await SendAsync(new GetChatThreadsQuery())).TotalCount.Should().Be(0);

        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Not yours"
        })).Should().ThrowAsync<NotFoundException>();
    }

    // ---------------------------------------------------------------------
    // Threads held on a reservation
    // ---------------------------------------------------------------------

    /// <summary>A hold, optionally linked to a marketplace customer account.</summary>
    private async Task<int> ReservationAsync(
        string matricule,
        ReservationStatus status = ReservationStatus.Confirmed,
        string? marketplaceUserId = null)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        var client = new Client
        {
            FirstName = "Hold",
            LastName = "Client",
            MarketplaceUserId = marketplaceUserId
        };
        await AddAsync(client);

        var hold = Reservation.Create(
            car.Id, Start, End, Money.Of(200m, "TND"), expiresAt: Start.AddDays(-1), client.Id);

        if (status is ReservationStatus.Confirmed or ReservationStatus.Paid)
        {
            hold.Confirm();
        }

        await AddAsync(hold);

        return hold.Id;
    }

    /// <summary>
    /// The questions that matter most — what to bring, where to collect — are
    /// asked before the hire exists, so the thread opens on the hold.
    /// </summary>
    [Test]
    public async Task AConfirmedHoldCarriesItsOwnThread()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var reservationId = await ReservationAsync("CH-HOLD");

        await SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Reservation, Id = reservationId, Body = "Bring your licence"
        });

        var messages = await SendAsync(
            new GetChatMessagesQuery(ChatSubjectKind.Reservation, reservationId));

        messages.Should().ContainSingle();
        messages[0].Subject.Should().Be(ChatSubjectKind.Reservation);
        messages[0].ReservationId.Should().Be(reservationId);
        messages[0].RentingId.Should().BeNull();
    }

    /// <summary>
    /// A request still waiting for an answer is not a conversation: the answer to
    /// it is Confirm or Reject, not a message.
    /// </summary>
    [Test]
    public async Task APendingRequestIsNotOpenToMessages()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var reservationId = await ReservationAsync(
            "CH-PENDING", ReservationStatus.PendingConfirmation);

        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Reservation, Id = reservationId, Body = "Too early"
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task TheInboxListsHiresAndHoldsTogether()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var rentingId = await RentingAsync("CH-BOTH-R");
        var reservationId = await ReservationAsync("CH-BOTH-H");

        var threads = await SendAsync(new GetChatThreadsQuery());

        threads.Items.Should().HaveCount(2);
        threads.Items.Should().Contain(t =>
            t.Subject == ChatSubjectKind.Renting && t.RentingId == rentingId);
        threads.Items.Should().Contain(t =>
            t.Subject == ChatSubjectKind.Reservation && t.ReservationId == reservationId);
    }

    [Test]
    public async Task ACustomerCanAnswerOnTheirOwnHold()
    {
        var customerId = await RunAsUserAsync("holdchat@local", "Customer1234!", new[] { Roles.Customer });
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var reservationId = await ReservationAsync(
            "CH-HOLD-CUST", ReservationStatus.Confirmed, marketplaceUserId: customerId);

        await SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Reservation, Id = reservationId, Body = "Confirmed for Friday"
        });

        SetCurrentUser(customerId);
        SetCurrentAgency(null); // a customer has no tenant of their own

        var mine = await SendAsync(new GetMyChatThreadsQuery());
        mine.Should().ContainSingle()
            .Which.Subject.Should().Be(ChatSubjectKind.Reservation);

        await SendAsync(new SendCustomerChatMessageCommand
        {
            Subject = ChatSubjectKind.Reservation, Id = reservationId, Body = "See you then"
        });

        var messages = await SendAsync(
            new GetMyChatMessagesQuery(ChatSubjectKind.Reservation, reservationId));

        messages.Should().HaveCount(2);
        messages.Last().AuthorKind.Should().Be(ChatAuthorKind.Client);
    }

    [Test]
    public async Task ACustomerCannotReadSomebodyElsesHoldThread()
    {
        var ownerId = await RunAsUserAsync("holdowner@local", "Customer1234!", new[] { Roles.Customer });
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var reservationId = await ReservationAsync(
            "CH-HOLD-PRIV", ReservationStatus.Confirmed, marketplaceUserId: ownerId);

        await SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Reservation, Id = reservationId, Body = "Private"
        });

        await RunAsUserAsync("holdnosy@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        (await SendAsync(new GetMyChatMessagesQuery(ChatSubjectKind.Reservation, reservationId)))
            .Should().BeEmpty();

        await FluentActions.Invoking(() => SendAsync(new SendCustomerChatMessageCommand
        {
            Subject = ChatSubjectKind.Reservation, Id = reservationId, Body = "Let me in"
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ReadingIsGrantableWithoutTheRightToAnswer()
    {
        await RunAsAgencyStaffAsync(Permissions.ChatView);
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-VIEWONLY");

        // Chat.View alone reads the inboxâ€¦
        await FluentActions.Invoking(() => SendAsync(new GetChatThreadsQuery()))
            .Should().NotThrowAsync();

        // â€¦but does not speak for the agency.
        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            Subject = ChatSubjectKind.Renting, Id = rentingId, Body = "Blocked"
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }
}

