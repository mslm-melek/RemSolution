using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure.Data;

namespace RemSolution.Application.FunctionalTests.Documents;

using static Testing;

/// <summary>
/// Runs the demo seeder against a real database. It writes through every
/// interceptor (tenant stamp, subscription enforcement, audit) and calls the
/// document service for real, so this is the only way to know it works before
/// someone starts the app and finds out.
/// <para>
/// Also pins the two properties that make it safe to leave switched on in
/// development: it produces data consistent with the app's own rules, and running
/// it twice does not duplicate anything.
/// </para>
/// </summary>
public class DemoDataSeederTests : BaseTestFixture
{
    private const string PrimaryAgency = "Carthage Rent Tunis";
    private const string SecondaryAgency = "Sahara Cars Djerba";

    [Test]
    public async Task ShouldSeedBothAgenciesWithSomethingToClickThrough()
    {
        await SeedDemoDataAsync();

        var carthage = await FindAgencyAsync(PrimaryAgency);
        var sahara = await FindAgencyAsync(SecondaryAgency);

        carthage.Should().NotBeNull();
        sahara.Should().NotBeNull();

        using (AmbientTenant.Push(carthage!.Id))
        {
            (await CountAsync<Car>()).Should().Be(12);
            (await CountAsync<Client>()).Should().Be(15);
            (await CountAsync<Renting>()).Should().Be(20);
            (await CountAsync<Reservation>()).Should().Be(6);
            (await CountAsync<Branch>()).Should().Be(2);
            (await CountAsync<DocumentTemplate>()).Should().Be(2);

            // Money movements, extras and expenses all present.
            (await CountAsync<ExtraService>()).Should().BeGreaterThan(0);
            (await CountAsync<Payment>()).Should().BeGreaterThan(0);
            (await CountAsync<Expense>()).Should().BeGreaterThan(0);
        }

        using (AmbientTenant.Push(sahara!.Id))
        {
            (await CountAsync<Car>()).Should().Be(4);
            (await CountAsync<Client>()).Should().Be(5);
            (await CountAsync<Renting>()).Should().Be(3);
        }
    }

    /// <summary>
    /// The point of the shipped dataset is paperwork you can open immediately, and
    /// generating it exercises numbering, rendering and file storage for real.
    /// </summary>
    [Test]
    public async Task ShouldIssueContractsAndInvoicesWithArchivedPdfs()
    {
        await SeedDemoDataAsync();

        var carthage = await FindAgencyAsync(PrimaryAgency);

        using (AmbientTenant.Push(carthage!.Id))
        {
            (await CountAsync<Contract>()).Should().Be(3);
            (await CountAsync<Facture>()).Should().Be(2);

            var contracts = await AllAsync<Contract>();

            // Numbering ran per agency and per year, in sequence.
            contracts.Select(c => c.SequenceNumber).OrderBy(n => n).Should().Equal(1, 2, 3);
            contracts.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Number));

            // One of them came from the custom long-stay template, the rest from the
            // agency default — so both resolution paths are represented.
            contracts.Select(c => c.TemplateName).Should().Contain("Contrat Carthage — longue durée");

            foreach (var contract in contracts)
            {
                var file = await FindAsync<StoredFile>(contract.DocumentFileId);

                file.Should().NotBeNull();
                file!.DocumentType.Should().Be(DocumentType.RentalContract);
                file.Size.Should().BeGreaterThan(1000, "a rendered A4 contract is a few KB");

                File.Exists(Path.Combine(UploadsRoot, file.Path.Replace('/', Path.DirectorySeparatorChar)))
                    .Should().BeTrue("the PDF must exist on disk, not just as a row");
            }
        }
    }

    /// <summary>
    /// Restarting the app must not double the dataset — the seeder runs on every
    /// development startup.
    /// </summary>
    [Test]
    public async Task ShouldNotDuplicateAnythingWhenRunTwice()
    {
        await SeedDemoDataAsync();
        await SeedDemoDataAsync();

        var agencies = await AllAsync<Agency>();
        agencies.Count(a => a.Name == PrimaryAgency).Should().Be(1);
        agencies.Count(a => a.Name == SecondaryAgency).Should().Be(1);

        var carthage = agencies.Single(a => a.Name == PrimaryAgency);

        using (AmbientTenant.Push(carthage.Id))
        {
            (await CountAsync<Car>()).Should().Be(12);
            (await CountAsync<Renting>()).Should().Be(20);
            (await CountAsync<Contract>()).Should().Be(3);
        }
    }

    /// <summary>
    /// Seeded bookings must obey the rule the app enforces: no two active bookings
    /// on one car may overlap. Fake data that breaks the domain's own invariant is
    /// worse than no fake data, because every availability screen then lies.
    /// </summary>
    [Test]
    public async Task ShouldNotSeedOverlappingBookingsForTheSameCar()
    {
        await SeedDemoDataAsync();

        foreach (var agency in await AllAsync<Agency>())
        {
            using var _ = AmbientTenant.Push(agency.Id);

            var active = (await AllAsync<Renting>())
                .Where(r => r.RentingState != RentingState.Cancelled)
                .ToList();

            foreach (var group in active.GroupBy(r => r.CarId))
            {
                var windows = group
                    .OrderBy(r => r.StartDate)
                    .Select(r => (Start: r.StartDate!.Value, End: r.EndDate!.Value))
                    .ToList();

                for (var i = 1; i < windows.Count; i++)
                {
                    // Half-open ranges: back-to-back (previous end == next start) is
                    // fine, genuine overlap is not.
                    windows[i].Start.Should().BeOnOrAfter(windows[i - 1].End,
                        $"car {group.Key} has overlapping seeded bookings");
                }
            }
        }
    }

    /// <summary>
    /// The second agency is on Starter precisely so the feature gate is visible, and
    /// the staff login is under-permissioned on purpose. Both are easy to break by
    /// "tidying" the seeder, so they are pinned.
    /// </summary>
    [Test]
    public async Task ShouldGiveTheTwoAgenciesDifferentEntitlements()
    {
        await SeedDemoDataAsync();

        var plans = await AllAsync<SubscriptionPlan>();

        var starter = plans.Single(p => p.Name == "Starter");
        var full = plans.Single(p => p.Name == "Full");

        var starterFeatures = await FeaturesOfAsync(starter.Id);
        var fullFeatures = await FeaturesOfAsync(full.Id);

        starterFeatures.Should().NotContain(FeatureFlags.Contracts);
        starterFeatures.Should().NotContain(FeatureFlags.Factures);
        starterFeatures.Should().NotContain(FeatureFlags.Payments);
        starterFeatures.Should().Contain(FeatureFlags.Rentings);

        fullFeatures.Should().Contain(FeatureFlags.Contracts);
        fullFeatures.Should().Contain(FeatureFlags.Factures);
    }

    [Test]
    public async Task ShouldCreateALoginForEveryRole()
    {
        await SeedDemoDataAsync();

        foreach (var (email, role) in new[]
                 {
                     ("admin@demo.tn", Roles.AgencyAdministrator),
                     ("staff@demo.tn", Roles.AgencyStaff),
                     ("admin@sahara.tn", Roles.AgencyAdministrator),
                     ("customer@demo.tn", Roles.Customer),
                 })
        {
            var userId = await UserIdAsync(email);

            userId.Should().NotBeNull($"{email} must exist");
            (await IsInRoleAsync(userId!, role)).Should().BeTrue($"{email} must be {role}");
        }

        // The staff login is deliberately short of Client.Create and
        // Contract.Generate so the refusal paths can be exercised by logging in.
        var staffId = await UserIdAsync("staff@demo.tn");
        var granted = (await AllAsync<UserPermission>())
            .Where(p => p.UserId == staffId)
            .Select(p => p.Permission)
            .ToList();

        granted.Should().Contain(Permissions.RentingCreate);
        granted.Should().Contain(Permissions.ContractRead);
        granted.Should().NotContain(Permissions.ClientCreate);
        granted.Should().NotContain(Permissions.ContractGenerate);
    }

    /// <summary>
    /// One client carries a known CIN so the renting form's new-client dedup can be
    /// tried by hand. If that document goes missing the fixture is useless.
    /// </summary>
    [Test]
    public async Task ShouldSeedTheClientTheDedupFixtureDependsOn()
    {
        await SeedDemoDataAsync();

        var carthage = await FindAgencyAsync(PrimaryAgency);

        using var _ = AmbientTenant.Push(carthage!.Id);

        var clients = await AllAsync<Client>();

        clients.Should().ContainSingle(c => c.CIN == "09887766")
            .Which.LastName.Should().Be("Ben Salah");

        clients.Should().Contain(c => c.IsFlagged, "a flagged client makes the risk signal visible");
        clients.Should().Contain(c => c.CIN == null && c.PasseportNumber != null,
            "a passport-only renter exercises the foreign-client path");
    }

    private static Task SeedDemoDataAsync() =>
        UsingScopeAsync(async provider =>
        {
            await provider.GetRequiredService<DemoDataSeeder>().SeedAsync();
            return true;
        });

    private static async Task<Agency?> FindAgencyAsync(string name) =>
        (await AllAsync<Agency>()).SingleOrDefault(a => a.Name == name);

    private static async Task<List<string>> FeaturesOfAsync(int planId) =>
        (await AllAsync<PlanFeature>())
            .Where(f => f.PlanId == planId)
            .Select(f => f.Feature)
            .ToList();
}
