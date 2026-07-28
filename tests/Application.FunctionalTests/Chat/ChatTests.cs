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

        var renting = new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = Start,
            EndDate = End,
            RentingState = state,
            Price = Money.Of(200m, "TND")
        };
        await AddAsync(renting);

        return renting.Id;
    }

    [Test]
    public async Task AgencyMessageIsStoredAsTheAgencySide()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-1");

        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "  Your car is ready  " });

        var messages = await SendAsync(new GetChatMessagesQuery(rentingId));
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
        await SendAsync(new SendChatMessageCommand { RentingId = ongoing, Body = "Hello" });

        var finished = await RentingAsync("CH-DONE", RentingState.Done);

        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            RentingId = finished, Body = "Too late"
        })).Should().ThrowAsync<ValidationException>();

        // The open thread is unaffected and still readable.
        (await SendAsync(new GetChatMessagesQuery(ongoing))).Should().HaveCount(1);
    }

    [Test]
    public async Task TheAfterIdCursorReturnsOnlyNewMessages()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-2");

        var firstId = await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "One" });
        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "Two" });

        var incoming = await SendAsync(new GetChatMessagesQuery(rentingId, AfterId: firstId));

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
        await SendAsync(new SendChatMessageCommand { RentingId = talking, Body = "Hi" });

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
        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "Welcome" });

        // Act as the customer: signed in, and with NO tenant — exactly what a
        // marketplace account looks like, so the cross-tenant path is exercised.
        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        var mine = await SendAsync(new GetMyChatMessagesQuery(rentingId));
        mine.Should().HaveCount(1);

        await SendAsync(new SendCustomerChatMessageCommand { RentingId = rentingId, Body = "Thanks!" });

        // Back on the agency side: the reply is there and counts as unread.
        SetCurrentUser(adminId);
        SetCurrentAgency(agencyId);

        var threads = await SendAsync(new GetChatThreadsQuery());
        var thread = threads.Items.Single(x => x.RentingId == rentingId);
        thread.UnreadCount.Should().Be(1);
        thread.LastMessageAuthorKind.Should().Be(ChatAuthorKind.Client);

        await SendAsync(new MarkChatReadCommand(rentingId));

        var afterRead = await SendAsync(new GetChatThreadsQuery());
        afterRead.Items.Single(x => x.RentingId == rentingId).UnreadCount.Should().Be(0);
    }

    [Test]
    public async Task MarkReadOnlyStampsTheOtherSidesMessages()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-READ");
        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "From the desk" });

        await SendAsync(new MarkChatReadCommand(rentingId));

        var messages = await SendAsync(new GetChatMessagesQuery(rentingId));
        // The agency never marks its own message read — ReadAt means the
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
        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "Please confirm" });

        SetCurrentUser(customerId);
        SetCurrentAgency(null);
        await SendAsync(new MarkMyChatReadCommand(rentingId));

        var mine = await SendAsync(new GetMyChatMessagesQuery(rentingId));
        mine.Single().ReadAt.Should().NotBeNull();
    }

    [Test]
    public async Task ACustomerCannotReadOrPostInSomeoneElsesThread()
    {
        var ownerId = await RunAsUserAsync("chatowner@local", "Customer1234!", new[] { Roles.Customer });

        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-PRIVATE", marketplaceUserId: ownerId);
        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "Private" });

        // A different customer account, with no tenant of its own.
        await RunAsUserAsync("chatstranger@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        (await SendAsync(new GetMyChatMessagesQuery(rentingId))).Should().BeEmpty();
        (await SendAsync(new GetMyChatThreadsQuery())).Should().BeEmpty();

        await FluentActions.Invoking(() => SendAsync(new SendCustomerChatMessageCommand
        {
            RentingId = rentingId, Body = "Let me in"
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task AnotherAgencyCannotSeeOrPostInTheThread()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-TENANT");
        await SendAsync(new SendChatMessageCommand { RentingId = rentingId, Body = "Ours" });

        await AddTestAgencyAsync(); // second tenant

        (await SendAsync(new GetChatMessagesQuery(rentingId))).Should().BeEmpty();
        (await SendAsync(new GetChatThreadsQuery())).TotalCount.Should().Be(0);

        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            RentingId = rentingId, Body = "Not yours"
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ReadingIsGrantableWithoutTheRightToAnswer()
    {
        await RunAsAgencyStaffAsync(Permissions.ChatView);
        await AddTestAgencyAsync();
        var rentingId = await RentingAsync("CH-VIEWONLY");

        // Chat.View alone reads the inbox…
        await FluentActions.Invoking(() => SendAsync(new GetChatThreadsQuery()))
            .Should().NotThrowAsync();

        // …but does not speak for the agency.
        await FluentActions.Invoking(() => SendAsync(new SendChatMessageCommand
        {
            RentingId = rentingId, Body = "Blocked"
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }
}
