using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace RemSolution.Application.FunctionalTests.Clients;

using static Testing;

// Proves the P.12 accent-/case-insensitive collation on client name columns:
// an ASCII, lower-case query matches an accented, capitalised stored name.
public class ClientSearchCollationTests : BaseTestFixture
{
    [Test]
    public async Task NameComparisonIgnoresAccentsAndCase()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(new CreateClientCommand
        {
            FirstName = "Amélie",
            LastName = "Crémüs",
            BirthDate = new DateTime(1991, 2, 3)
        });

        var matches = await UsingScopeAsync(sp =>
        {
            var context = sp.GetRequiredService<ApplicationDbContext>();
            // No accents, different case — matches only under CI_AI collation.
            return context.Clients.CountAsync(c => c.FirstName == "amelie" && c.LastName == "CREMUS");
        });

        matches.Should().Be(1);
    }
}
