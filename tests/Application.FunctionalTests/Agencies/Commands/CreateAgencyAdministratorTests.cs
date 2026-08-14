using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

// The administrator login is created with the agency, in the same transaction.
public class CreateAgencyAdministratorTests : BaseTestFixture
{
    private static async Task<int> ACountryAsync()
    {
        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        return country.Id;
    }

    private static Task<ApplicationUser?> UserAsync(string userName) =>
        UsingScopeAsync(async provider =>
            await provider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(userName));

    [Test]
    public async Task ShouldCreateAnAdministratorWhoCanSignIn()
    {
        await RunAsPlatformAdministratorAsync();
        var countryId = await ACountryAsync();

        var created = await SendAsync(new CreateAgencyCommand
        {
            Name = "Sud Cars",
            CountryId = countryId,
            AdminEmail = "patron@sudcars.test",
            AdminFullName = "Leïla Ben Salah"
        });

        created.AdminUserName.Should().Be("patron@sudcars.test");
        created.AdminTemporaryPassword.Should().NotBeNullOrWhiteSpace();

        var user = await UserAsync("patron@sudcars.test");

        user.Should().NotBeNull();
        user!.AgencyId.Should().Be(created.Id);
        user.Email.Should().Be("patron@sudcars.test");
        user.FullName.Should().Be("Leïla Ben Salah");
        // A temporary password: the account can only replace it until it does.
        user.MustChangePassword.Should().BeTrue();

        (await IsInRoleAsync(user.Id, Roles.AgencyAdministrator)).Should().BeTrue();

        // The password returned is the one the account holds; it is handed to a human.
        var signsIn = await UsingScopeAsync(provider =>
            provider.GetRequiredService<UserManager<ApplicationUser>>()
                .CheckPasswordAsync(user, created.AdminTemporaryPassword!));

        signsIn.Should().BeTrue();
    }

    [Test]
    public async Task ShouldFallBackToTheAgencyEmailWhenNoAdministratorAddressIsGiven()
    {
        await RunAsPlatformAdministratorAsync();
        var countryId = await ACountryAsync();

        var created = await SendAsync(new CreateAgencyCommand
        {
            Name = "Contact address only",
            CountryId = countryId,
            Email = "contact@onlyaddress.test"
        });

        created.AdminUserName.Should().Be("contact@onlyaddress.test");
        (await UserAsync("contact@onlyaddress.test"))!.AgencyId.Should().Be(created.Id);
    }

    [Test]
    public async Task ShouldRefuseAnAgencyWithNoAddressAtAll()
    {
        await RunAsPlatformAdministratorAsync();
        var countryId = await ACountryAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCommand
        {
            Name = "Nobody can open me",
            CountryId = countryId
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<Agency>()).Should().Be(0);
    }

    // An agency whose administrator could not be created must not survive.
    [Test]
    public async Task ShouldRollTheAgencyBackWhenTheAddressIsAlreadyAnAccount()
    {
        await RunAsPlatformAdministratorAsync();
        var countryId = await ACountryAsync();

        await SendAsync(new CreateAgencyCommand
        {
            Name = "First",
            CountryId = countryId,
            AdminEmail = "shared@example.test"
        });

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCommand
        {
            Name = "Second, same address",
            CountryId = countryId,
            AdminEmail = "shared@example.test"
        })).Should().ThrowAsync<ValidationException>();

        (await AllAsync<Agency>()).Should().ContainSingle(a => a.Name == "First");
        (await CountAsync<Agency>()).Should().Be(1);
    }

    // Tests have no SMTP host, so the logging sender reports the mail as sent.
    [Test]
    public async Task ShouldReportWhetherTheWelcomeMailWasSent()
    {
        await RunAsPlatformAdministratorAsync();
        var countryId = await ACountryAsync();

        var created = await SendAsync(new CreateAgencyCommand
        {
            Name = "Mailed",
            CountryId = countryId,
            AdminEmail = "patron@mailed.test"
        });

        created.WelcomeEmailSent.Should().BeTrue();
    }
}
