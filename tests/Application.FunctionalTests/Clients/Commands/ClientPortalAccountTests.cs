using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.InviteClientCommand;
using RemSolution.Application.Features.Client.Commands.UpdateClientCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

/// <summary>
/// Recording an email against a client gives them a customer-portal login.
/// These cover the four answers that path can give — created, linked, refused,
/// re-issued — because they differ in whether a password is generated, and a
/// password generated for the wrong account is the failure that matters here.
/// </summary>
public class ClientPortalAccountTests : BaseTestFixture
{
    private static readonly DateTime BirthDate = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 1, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldProvisionCustomerAccountWhenClientHasEmail()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient("leila@example.tn"));

        var client = await FindAsync<Client>(id);
        client!.MarketplaceUserId.Should().NotBeNullOrEmpty();

        var user = await UserAsync(client.MarketplaceUserId!);
        user.Should().NotBeNull();
        user!.Email.Should().Be("leila@example.tn");
        user.UserName.Should().Be("leila@example.tn");
        // Not tenant-scoped: one customer identity, a Client row per agency.
        user.AgencyId.Should().BeNull();
        // The password came from us, so the account is confined to replacing it.
        user.MustChangePassword.Should().BeTrue();

        (await IsInRoleAsync(user.Id, Roles.Customer)).Should().BeTrue();
    }

    // Identity's default AllowedUserNameCharacters is ASCII-only, and the login
    // here IS the email — so an accented local part would fail account creation
    // for an address the agency typed correctly. AddDefaultIdentity turns that
    // check off; this is what says so.
    [Test]
    public async Task ShouldProvisionForAnAccentedEmailAddress()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient("josé.benali@example.tn"));

        var client = await FindAsync<Client>(id);
        client!.MarketplaceUserId.Should().NotBeNullOrEmpty();

        var user = await UserAsync(client.MarketplaceUserId!);
        user!.UserName.Should().Be("josé.benali@example.tn");
    }

    [Test]
    public async Task ShouldNotProvisionAccountWithoutEmail()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient(email: null));

        var client = await FindAsync<Client>(id);
        client!.MarketplaceUserId.Should().BeNull();
    }

    [Test]
    public async Task ShouldLinkExistingCustomerAccountRatherThanCreateASecond()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // A customer who already registered on the marketplace and chose their
        // own password.
        var existingId = await SeedCustomerAsync("returning@example.tn", mustChangePassword: false);

        var id = await SendAsync(NewClient("returning@example.tn"));

        var client = await FindAsync<Client>(id);
        client!.MarketplaceUserId.Should().Be(existingId);

        // Their password is untouched — the agency adopted the identity, it did
        // not take it over.
        var user = await UserAsync(existingId);
        user!.MustChangePassword.Should().BeFalse();
    }

    [Test]
    public async Task ShouldRefuseToLinkAStaffEmail()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // The agency's own admin login, typed into a client record.
        var staffEmail = "agencyadmin@local";
        (await UserIdAsync(staffEmail)).Should().NotBeNull();

        var id = await SendAsync(NewClient(staffEmail));

        // The client is created, but nothing is linked: an operator login must
        // not double as a customer identity.
        var client = await FindAsync<Client>(id);
        client!.Email.Should().Be(staffEmail);
        client.MarketplaceUserId.Should().BeNull();
    }

    [Test]
    public async Task ShouldProvisionWhenTheEmailIsAddedLater()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient(email: null));

        var before = await FindAsync<Client>(id);
        before!.MarketplaceUserId.Should().BeNull();

        await SendAsync(new UpdateClientCommand
        {
            Id = id,
            RowVersion = before.RowVersion,
            FirstName = before.FirstName!,
            LastName = before.LastName!,
            Email = "later@example.tn",
            BirthDate = before.BirthDate,
        });

        var after = await FindAsync<Client>(id);
        after!.MarketplaceUserId.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ShouldNotRelinkWhenTheEmailIsEdited()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient("first@example.tn"));
        var linked = (await FindAsync<Client>(id))!;
        var originalUserId = linked.MarketplaceUserId;

        await SendAsync(new UpdateClientCommand
        {
            Id = id,
            RowVersion = linked.RowVersion,
            FirstName = linked.FirstName!,
            LastName = linked.LastName!,
            Email = "corrected@example.tn",
            BirthDate = linked.BirthDate,
        });

        // The contact field moved; the identity the customer signs in with did
        // not, and no second account was minted for the new address.
        var after = await FindAsync<Client>(id);
        after!.Email.Should().Be("corrected@example.tn");
        after.MarketplaceUserId.Should().Be(originalUserId);

        (await UserIdAsync("corrected@example.tn")).Should().BeNull();
    }

    [Test]
    public async Task ShouldProvisionForAClientCreatedInlineWithARenting()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car
        {
            Matricule = "PORTAL-1",
            Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"),
        };
        await AddAsync(car);

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id,
            NewClient = new NewRentingClient
            {
                FirstName = "Walk",
                LastName = "In",
                Email = "walkin@example.tn",
                BirthDate = BirthDate,
            },
            StartDate = Start,
            EndDate = End,
        });

        var renting = await FindAsync<Renting>(rentingId);
        var client = await FindAsync<Client>(renting!.ClientId!.Value);

        client!.Email.Should().Be("walkin@example.tn");
        client.MarketplaceUserId.Should().NotBeNullOrEmpty();

        var user = await UserAsync(client.MarketplaceUserId!);
        user!.MustChangePassword.Should().BeTrue();
    }

    [Test]
    public async Task ShouldReissueTheTemporaryPasswordForAnUnusedAccount()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient("lostmail@example.tn"));

        var before = await UserAsync((await FindAsync<Client>(id))!.MarketplaceUserId!);
        var originalHash = before!.PasswordHash;

        var result = await SendAsync(new InviteClientCommand(id));

        result.Outcome.Should().Be(ClientAccountOutcome.PasswordReset);

        var after = await UserAsync(before.Id);
        after!.PasswordHash.Should().NotBe(originalHash);
        after.MustChangePassword.Should().BeTrue();
    }

    [Test]
    public async Task ShouldNotResetThePasswordOfAnAccountTheCustomerOwns()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var existingId = await SeedCustomerAsync("settled@example.tn", mustChangePassword: false);
        var originalHash = (await UserAsync(existingId))!.PasswordHash;

        var id = await SendAsync(NewClient("settled@example.tn"));

        var result = await SendAsync(new InviteClientCommand(id));

        // The agency may link to this identity but never lock its owner out of
        // it — that is what the forgotten-password flow is for.
        result.Outcome.Should().Be(ClientAccountOutcome.AlreadyActive);
        result.EmailSent.Should().BeFalse();

        var after = await UserAsync(existingId);
        after!.PasswordHash.Should().Be(originalHash);
        after.MustChangePassword.Should().BeFalse();
    }

    [Test]
    public async Task ShouldReportNothingToDoWhenTheClientHasNoEmail()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(NewClient(email: null));

        var result = await SendAsync(new InviteClientCommand(id));

        result.Outcome.Should().Be(ClientAccountOutcome.None);
        result.EmailSent.Should().BeFalse();
    }

    private static CreateClientCommand NewClient(string? email) => new()
    {
        FirstName = "Portal",
        LastName = "Client",
        Email = email,
        BirthDate = BirthDate,
    };

    private static Task<ApplicationUser?> UserAsync(string userId) =>
        UsingScopeAsync(async services =>
            await services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId));

    /// <summary>
    /// A customer account that exists before the agency ever records the
    /// address — the marketplace self-registration case.
    /// </summary>
    private static Task<string> SeedCustomerAsync(string email, bool mustChangePassword) =>
        UsingScopeAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                MustChangePassword = mustChangePassword,
            };

            var created = await userManager.CreateAsync(user, "TheirOwn1!");
            created.Succeeded.Should().BeTrue();

            var roled = await userManager.AddToRoleAsync(user, Roles.Customer);
            roled.Succeeded.Should().BeTrue();

            return user.Id;
        });
}
