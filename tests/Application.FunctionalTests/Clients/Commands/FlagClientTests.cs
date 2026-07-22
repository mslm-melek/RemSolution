using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.FlagClientCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class FlagClientTests : BaseTestFixture
{
    private async Task<int> CreateClientAsync()
    {
        await AddTestAgencyAsync();

        return await SendAsync(new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20)
        });
    }

    [Test]
    public async Task ShouldFlagClientWithReason()
    {
        await RunAsAgencyAdministratorAsync();
        var clientId = await CreateClientAsync();

        await SendAsync(new FlagClientCommand
        {
            Id = clientId,
            IsFlagged = true,
            Notes = "Bounced two payments"
        });

        var client = await FindAsync<Client>(clientId);

        client!.IsFlagged.Should().BeTrue();
        client.Notes.Should().Be("Bounced two payments");
    }

    [Test]
    public async Task ShouldClearFlag()
    {
        await RunAsAgencyAdministratorAsync();
        var clientId = await CreateClientAsync();

        await SendAsync(new FlagClientCommand
        {
            Id = clientId,
            IsFlagged = true,
            Notes = "Bounced two payments"
        });

        await SendAsync(new FlagClientCommand
        {
            Id = clientId,
            IsFlagged = false,
            Notes = null
        });

        var client = await FindAsync<Client>(clientId);

        client!.IsFlagged.Should().BeFalse();
        client.Notes.Should().BeNull();
    }

    [Test]
    public async Task ShouldRequireReasonWhenFlagging()
    {
        await RunAsAgencyAdministratorAsync();
        var clientId = await CreateClientAsync();

        var command = new FlagClientCommand
        {
            Id = clientId,
            IsFlagged = true
            // Notes missing
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldThrowNotFoundForUnknownClient()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = new FlagClientCommand
        {
            Id = 999999,
            IsFlagged = true,
            Notes = "No such client"
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }
}
