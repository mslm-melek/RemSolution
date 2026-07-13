using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Queries.GetClientByIdQuery;

namespace RemSolution.Application.FunctionalTests.Clients.Queries;

using static Testing;

public class GetClientByIdQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnClientById()
    {
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            CIN = "AB123456"
        });

        var result = await SendAsync(new GetClientByIdQuery(clientId));

        result.Should().NotBeNull();
        result!.FirstName.Should().Be("John");
        result.CIN.Should().Be("AB123456");
    }
}
