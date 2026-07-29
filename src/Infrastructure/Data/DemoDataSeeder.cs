using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Infrastructure.Data;

/// <summary>
/// Fills a development database with a coherent dataset you can actually click
/// through: five agencies across four countries and three currencies, on
/// different plans, with logins for every role, branches placed on the map,
/// priced cars, clients, bookings in every state, money movements, reservations
/// in every status, document templates and a few already-issued PDFs.
/// <para>
/// Design notes:
/// </para>
/// <list type="bullet">
/// <item><b>Opt-in.</b> Runs only when <see cref="DemoDataOptions.Enabled"/> is set
/// AND the host is Development (see InitialiserExtensions).</item>
/// <item><b>One currency per agency.</b> Amounts are a Money value object
/// carrying their ISO code, and the agency's AgencySettings row is what the app
/// reads them back against — so the Casablanca agency's cars are priced in MAD,
/// not in the Tunisian dinar the first two agencies use. <see cref="ActAs"/>
/// switches both the tenant and the currency together.</item>
/// <item><b>Branches carry an address AND a pin.</b> Both are what the map
/// picker writes, and the pin is what puts an agency on the marketplace map —
/// a branch without one is invisible to search by distance.</item>
/// <item><b>Idempotent.</b> Presence of the first demo agency means "already
/// seeded" and the whole thing is skipped, so restarting the app does not
/// duplicate anything. To start over, drop the database
/// (<c>dotnet ef database drop -f -p src/Infrastructure -s src/Web</c>) and run
/// again — deleting piecemeal would fight the Restrict FKs that protect financial
/// records, which is the schema working as intended.</item>
/// <item><b>Tenant context via <see cref="AmbientTenant"/>.</b> Both the tenant
/// stamp and the query filters read the ambient tenant, so pushing it makes the
/// seeder behave exactly like a request for that agency instead of hand-setting
/// AgencyId everywhere and reading through filters that match nothing.</item>
/// <item><b>Honest data.</b> Bookings for the same car never overlap, so the
/// availability rule stays true of the seeded data; prices come from
/// <see cref="IPricingService"/> rather than a second copy of the arithmetic.</item>
/// </list>
/// </summary>
public class DemoDataSeeder
{
    // Presence of this agency is the "already seeded" marker.
    private const string PrimaryAgencyName = "Carthage Rent Tunis";
    private const string SecondaryAgencyName = "Sahara Cars Djerba";

    private const string DemoPassword = "Demo1234!";

    // ISO 4217 codes, one per agency. Every Money an agency stores is in its own
    // code — see _currency.
    private const string TunisianDinar = "TND";
    private const string MoroccanDirham = "MAD";
    private const string Euro = "EUR";
    private const string UaeDirham = "AED";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IPricingService _pricing;
    private readonly IRentalDocumentService _documents;
    private readonly TimeProvider _dateTime;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IPricingService pricing,
        IRentalDocumentService documents,
        TimeProvider dateTime,
        ILogger<DemoDataSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _pricing = pricing;
        _documents = documents;
        _dateTime = dateTime;
        _logger = logger;
    }

    /// <summary>Today at midnight UTC — every seeded date hangs off this.</summary>
    private DateTime Today => _dateTime.GetUtcNow().UtcDateTime.Date;

    /// <summary>
    /// The currency of the agency being seeded right now. Every Money the seeder
    /// writes has to be in its own agency's currency — the agency's
    /// AgencySettings row is what the app reads amounts back against, so a Paris
    /// agency priced in dinars would render as nonsense. Set by
    /// <see cref="ActAs"/> alongside the ambient tenant, and for the same
    /// reason: the helpers below then need no currency argument, and adding an
    /// agency cannot forget to pass one.
    /// </summary>
    private string _currency = "TND";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Agencies.AnyAsync(a => a.Name == PrimaryAgencyName, cancellationToken))
        {
            _logger.LogInformation("Demo data already present — skipping.");
            return;
        }

        _logger.LogInformation("Seeding demo data…");

        // Deliberately self-sufficient rather than assuming the base seeder ran:
        // roles and plans are created here if absent, so this can be pointed at an
        // empty database (which is also what makes it testable in isolation).
        await EnsureRolesAsync();

        // Global reference data first: brands, models and the type catalogs are
        // shared by every agency and carry no tenant.
        var models = await SeedCarCatalogAsync(cancellationToken);
        var extras = await SeedExtraServiceTypesAsync(cancellationToken);
        await SeedExpenseTypesAsync(cancellationToken);

        var tunisia = await CountryAsync("Tunisie", cancellationToken);

        // ---- Agency 1: everything switched on ----
        var full = await PlanAsync("Full", cancellationToken);
        var carthage = await SeedAgencyAsync(
            PrimaryAgencyName, "12 avenue Habib Bourguiba, Tunis",
            "+216 71 240 100", "contact@carthagerent.tn", tunisia, full,
            TunisianDinar, 36.7996, 10.1815, cancellationToken);

        await SeedUserAsync("admin@demo.tn", "Nadia Ben Amor", Roles.AgencyAdministrator, carthage.Id);

        // Staff deliberately WITHOUT Client.Create or Contract.Generate: that is
        // what makes the "pick an existing client but don't add one" and "read the
        // contract but don't issue it" paths testable without editing permissions.
        await SeedUserAsync("staff@demo.tn", "Karim Jelassi", Roles.AgencyStaff, carthage.Id, new[]
        {
            Permissions.CarRead,
            Permissions.ClientRead,
            Permissions.RentingCreate, Permissions.RentingRead, Permissions.RentingUpdate,
            Permissions.ReservationRead,
            Permissions.ExtraServiceRead,
            Permissions.PaymentCreate, Permissions.PaymentRead,
            Permissions.ContractRead,
            Permissions.FactureRead,
        });

        using (ActAs(carthage))
        {
            await SeedCarthageAsync(carthage, tunisia, models, extras, cancellationToken);
        }

        // ---- Agency 2: Starter plan, so Payments/ExtraServices/Contracts/Factures
        // are OFF. Useful for seeing the feature gate hide whole panels. ----
        var starter = await PlanAsync("Starter", cancellationToken);
        var sahara = await SeedAgencyAsync(
            SecondaryAgencyName, "Route de Midoun, Djerba",
            "+216 75 650 200", "contact@saharacars.tn", tunisia, starter,
            TunisianDinar, 33.8076, 10.9963, cancellationToken);

        await SeedUserAsync("admin@sahara.tn", "Hichem Gharbi", Roles.AgencyAdministrator, sahara.Id);

        using (ActAs(sahara))
        {
            await SeedSaharaAsync(sahara, tunisia, models, cancellationToken);
        }

        // ---- Agencies 3-5: other countries and other currencies. Each trades in
        // its own, which is what makes a cross-agency screen (the marketplace, the
        // platform dashboard) show prices that must not be added together — and
        // each spreads its branches over more than one city, so the map and the
        // "pick-up place" filters have something to work with. ----
        await SeedAtlasAsync(models, extras, full, cancellationToken);
        await SeedRivieraAsync(models, extras, full, cancellationToken);
        await SeedGulfDriveAsync(models, starter, cancellationToken);

        // A marketplace customer: no agency, browses across agencies.
        await SeedUserAsync("customer@demo.tn", "Leïla Msakni", Roles.Customer, agencyId: null);

        _logger.LogInformation(
            "Demo data seeded. Logins: admin@demo.tn / staff@demo.tn / admin@sahara.tn / " +
            "admin@atlasrent.ma / admin@riviera.fr / admin@gulfdrive.ae / customer@demo.tn " +
            "(password {Password}).",
            DemoPassword);
    }

    // ---------------------------------------------------------------- agency 1

    private async Task SeedCarthageAsync(
        Agency agency,
        Country country,
        IReadOnlyDictionary<string, ModelCar> models,
        IReadOnlyList<ExtraServicesType> extras,
        CancellationToken cancellationToken)
    {
        var centre = Branch("Agence Tunis Centre", country,
            "12 avenue Habib Bourguiba, Tunis", 36.7996, 10.1815);

        var airport = Branch("Aéroport Tunis-Carthage", country,
            "Aéroport Tunis-Carthage, Tunis", 36.8510, 10.2272);

        _context.Branches.AddRange(centre, airport);
        await _context.SaveChangesAsync(cancellationToken);

        // Mixed statuses: only Active cars are bookable, so Maintenance/Inactive
        // give you something to see excluded from availability.
        var cars = new[]
        {
            Car("184 TU 3021", models["Clio 5"], centre, 95m, "Blanc", 90, FuelType.Diesel, 2022),
            Car("184 TU 3022", models["Clio 5"], centre, 95m, "Gris", 90, FuelType.Diesel, 2022),
            Car("184 TU 4187", models["Symbol"], centre, 80m, "Blanc", 75, FuelType.Diesel, 2021),
            Car("192 TU 1140", models["Polo"], airport, 110m, "Bleu", 95, FuelType.Gasoline, 2023),
            Car("192 TU 1141", models["Golf 8"], airport, 165m, "Noir", 130, FuelType.Diesel, 2023),
            Car("178 TU 8802", models["208"], centre, 105m, "Rouge", 100, FuelType.Gasoline, 2022),
            Car("201 TU 2255", models["Picanto"], centre, 70m, "Blanc", 67, FuelType.Gasoline, 2024),
            Car("201 TU 2256", models["i10"], centre, 70m, "Argent", 67, FuelType.Gasoline, 2024),
            Car("196 TU 6644", models["Yaris"], airport, 120m, "Blanc", 100, FuelType.Gasoline, 2023),
            Car("188 TU 9310", models["Logan"], centre, 85m, "Beige", 90, FuelType.Diesel, 2021),
            Car("175 TU 5501", models["Duster"], airport, 175m, "Marron", 115, FuelType.Diesel, 2020,
                CarStatus.Maintenance),
            Car("169 TU 7788", models["Tucson"], centre, 220m, "Noir", 136, FuelType.Diesel, 2019,
                CarStatus.Inactive),
        };

        _context.Cars.AddRange(cars);
        await _context.SaveChangesAsync(cancellationToken);

        var clients = new[]
        {
            // The dedup fixture: type CIN 09887766 into the renting form's
            // "new client" panel and the booking should attach to THIS row
            // instead of creating a second Ben Salah.
            Client("Amina", "Ben Salah", 1990, 4, 12, "09887766", "K1234567", "12-345678"),
            Client("Mehdi", "Trabelsi", 1985, 9, 3, "09112233", null, "09-887711"),
            Client("Sonia", "Chaouch", 1993, 1, 27, "09445566", null, "13-220145"),
            Client("Youssef", "Khelifi", 1978, 6, 15, "08990011", "K7788990", "05-114477"),
            Client("Rania", "Bouzid", 1996, 11, 8, "09667788", null, "16-559002"),
            Client("Walid", "Hamdi", 1982, 3, 22, "08774455", null, "07-330891"),
            Client("Ines", "Sassi", 1999, 7, 4, "09223344", null, "18-771230"),
            Client("Tarek", "Mabrouk", 1974, 12, 30, "08551122", "K4455661", "02-908877"),
            Client("Nour", "Belhaj", 1991, 5, 19, "09338899", null, "12-664401"),
            Client("Slim", "Gharbi", 1988, 8, 11, "09001122", null, "10-445599"),
            Client("Dorra", "Ayari", 1994, 2, 6, "09556677", null, "14-887733"),
            Client("Anis", "Ferchichi", 1980, 10, 25, "08663344", null, "06-221100"),
            Client("Hela", "Zouari", 1997, 4, 2, "09779900", "K9900112", "17-334455"),
            // A foreign renter: passport only, no CIN.
            Client("Marc", "Lefèvre", 1975, 1, 14, null, "18AB55201", "751122334"),
            // Flagged: a risk signal the agency raised on its own record.
            Client("Bilel", "Nasri", 1986, 6, 9, "08882211", null, "08-556677",
                flagged: true, notes: "Véhicule rendu avec deux jours de retard et sans carburant (mars)."),
        };

        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync(cancellationToken);

        // Bookings, oldest first. Each car's windows are kept disjoint so the
        // overlap rule holds for the seeded data.
        var rentings = new List<Renting>
        {
            // --- finished, with mileage closed out ---
            Renting(cars[0], clients[0], -62, -58, RentingState.Done, 41_200, 41_940),
            Renting(cars[1], clients[1], -55, -52, RentingState.Done, 38_400, 38_910),
            Renting(cars[3], clients[2], -48, -41, RentingState.Done, 12_050, 13_480),
            Renting(cars[5], clients[3], -40, -37, RentingState.Done, 27_800, 28_260),
            Renting(cars[2], clients[4], -35, -30, RentingState.Done, 55_100, 56_020),
            Renting(cars[6], clients[5], -28, -25, RentingState.Done, 8_900, 9_310),
            Renting(cars[4], clients[7], -24, -17, RentingState.Done, 19_600, 21_150),
            Renting(cars[8], clients[8], -16, -12, RentingState.Done, 15_300, 16_040),
            Renting(cars[9], clients[6], -14, -10, RentingState.Done, 62_400, 63_100),
            Renting(cars[0], clients[13], -9, -5, RentingState.Done, 41_940, 42_600,
                notes: "Client étranger, permis international vérifié."),

            // --- cancelled, kept on the record ---
            Renting(cars[7], clients[9], -20, -18, RentingState.Cancelled, 5_200),
            Renting(cars[5], clients[14], -8, -6, RentingState.Cancelled, 28_260,
                notes: "Annulé par l'agence : client signalé."),

            // --- out on the road right now ---
            Renting(cars[1], clients[2], -2, +3, RentingState.InProgress, 38_910),
            Renting(cars[3], clients[10], -1, +4, RentingState.InProgress, 13_480),
            Renting(cars[8], clients[11], 0, +6, RentingState.InProgress, 16_040),

            // --- booked, not yet collected ---
            Renting(cars[0], clients[12], +2, +6, RentingState.NotYet),
            Renting(cars[4], clients[0], +3, +10, RentingState.NotYet,
                notes: "Livraison à l'aéroport, vol TU 720 à 14h.", secondDriver: clients[1]),
            Renting(cars[6], clients[4], +5, +8, RentingState.NotYet),
            Renting(cars[2], clients[6], +7, +12, RentingState.NotYet),
            Renting(cars[9], clients[8], +9, +14, RentingState.NotYet, secondDriver: clients[9]),
        };

        _context.Rentings.AddRange(rentings);
        await _context.SaveChangesAsync(cancellationToken);

        // Public ratings on SOME of the finished rentals — deliberately not all
        // of them: most customers never review, and an agency where every rental
        // carries five stars does not look like a real one. The spread is what
        // makes the shopfront's breakdown bars worth looking at.
        _context.AgencyReviews.AddRange(
            Review(agency, rentings[0], clients[0], "Renault Clio 5", 5, -57,
                "Voiture impeccable et prise en charge en dix minutes. Je relouerai."),
            Review(agency, rentings[2], clients[2], "Volkswagen Polo", 4, -40,
                "Bon rapport qualité-prix. Un peu d'attente au comptoir de l'aéroport."),
            Review(agency, rentings[4], clients[4], "Renault Symbol", 5, -29,
                "Équipe très arrangeante sur l'heure de retour."),
            Review(agency, rentings[6], clients[7], "Volkswagen Golf 8", 3, -16,
                "La voiture était bien, mais elle m'a été livrée avec un demi-réservoir."),
            Review(agency, rentings[8], clients[6], "Dacia Logan", 5, -9,
                "Rien à redire, tout était conforme au devis."),
            // A rating with no comment: stars alone are a complete review.
            Review(agency, rentings[9], clients[13], "Renault Clio 5", 4, -4, comment: null));

        await _context.SaveChangesAsync(cancellationToken);

        await SeedExtraServicesAsync(rentings, extras, cancellationToken);
        await SeedPaymentsAsync(rentings, cancellationToken);
        await SeedReservationsAsync(cars, clients, cancellationToken);
        await SeedExpensesAsync(cars, cancellationToken);
        await SeedTemplatesAndDocumentsAsync(rentings, cancellationToken);
    }

    private async Task SeedExtraServicesAsync(
        IReadOnlyList<Renting> rentings,
        IReadOnlyList<ExtraServicesType> extras,
        CancellationToken cancellationToken)
    {
        var gps = extras[0];
        var babySeat = extras[1];
        var secondDriver = extras[2];
        var insurance = extras[3];

        _context.ExtraServices.AddRange(
            Extra(rentings[2], gps),
            Extra(rentings[2], babySeat),
            Extra(rentings[6], insurance),
            Extra(rentings[6], secondDriver),
            Extra(rentings[9], gps),
            Extra(rentings[12], babySeat),
            Extra(rentings[13], gps),
            Extra(rentings[16], insurance),
            // A negotiated price that differs from the type's list amount.
            Extra(rentings[16], secondDriver, 45m));

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPaymentsAsync(IReadOnlyList<Renting> rentings, CancellationToken cancellationToken)
    {
        // Finished bookings are settled in full; in-progress ones carry a deposit;
        // upcoming ones are mostly unpaid, which is what makes the client-balance
        // and invoice "balance due" numbers interesting.
        var payments = new List<Payment>();

        foreach (var index in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
        {
            var renting = rentings[index];
            payments.Add(Payment(renting, renting.Price!.Amount, (index % 3) switch
            {
                0 => PaymentMethod.Cash,
                1 => PaymentMethod.Card,
                _ => PaymentMethod.Transfer
            }, DaysFromToday(renting.StartDate!.Value)));
        }

        // Part-payments on the live bookings.
        payments.Add(Payment(rentings[12], 200m, PaymentMethod.Cash, -2));
        payments.Add(Payment(rentings[13], 300m, PaymentMethod.Card, -1));
        payments.Add(Payment(rentings[14], 150m, PaymentMethod.Cash, 0));

        // A deposit on an upcoming booking.
        payments.Add(Payment(rentings[16], 400m, PaymentMethod.Transfer, 0));

        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync(cancellationToken);

        // A refund on the cancelled booking: money going back to the client, so a
        // negative amount rather than a deleted row.
        var cancelled = rentings[10];
        var refunded = Payment(cancelled, 240m, PaymentMethod.Cash, -21);
        _context.Payments.Add(refunded);
        await _context.SaveChangesAsync(cancellationToken);

        _context.Payments.Add(new Payment
        {
            ClientId = cancelled.ClientId,
            RentingId = cancelled.Id,
            PayementDate = Today.AddDays(-18),
            PayementAmount = Money.Of(-240m, _currency),
            Method = PaymentMethod.Cash,
            IsRefund = true,
            Notes = "Remboursement suite à l'annulation."
        });

        // A mistaken entry corrected by an offsetting reversal, which is how a
        // wrong payment is undone — the original stays on the record.
        var mistaken = Payment(rentings[8], 500m, PaymentMethod.Card, -11, "Saisie erronée.");
        _context.Payments.Add(mistaken);
        await _context.SaveChangesAsync(cancellationToken);

        _context.Payments.Add(new Payment
        {
            ClientId = mistaken.ClientId,
            RentingId = mistaken.RentingId,
            PayementDate = Today.AddDays(-11),
            PayementAmount = Money.Of(-500m, _currency),
            Method = PaymentMethod.Card,
            ReversesPaymentId = mistaken.Id,
            Notes = "Contrepassation de la saisie erronée."
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedReservationsAsync(
        IReadOnlyList<Car> cars, IReadOnlyList<Client> clients, CancellationToken cancellationToken)
    {
        // One hold per status, so every branch of the reservation state machine has
        // something behind it. Transitions go through the aggregate's own methods —
        // Status has a private setter precisely so nothing sets it directly.
        var expiryHours = 48;

        var pending = Hold(cars[6], clients[2], +12, +15, expiryHours);
        var confirmed = Hold(cars[7], clients[3], +14, +18, expiryHours);
        var paid = Hold(cars[5], clients[4], +16, +20, expiryHours);
        var rejected = Hold(cars[4], clients[5], +13, +16, expiryHours);
        var cancelled = Hold(cars[8], clients[6], +18, +22, expiryHours);
        var lapsed = Hold(cars[9], clients[7], +20, +24, -72);

        confirmed.Confirm();

        paid.Confirm();
        paid.MarkPaid();

        rejected.Reject("Véhicule déjà réservé pour cette période.");
        cancelled.Cancel("Annulée à la demande du client.");
        lapsed.Expire();

        _context.Reservations.AddRange(pending, confirmed, paid, rejected, cancelled, lapsed);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedExpensesAsync(IReadOnlyList<Car> cars, CancellationToken cancellationToken)
    {
        var types = await _context.ExpenseTypes.OrderBy(t => t.Id).ToListAsync(cancellationToken);

        if (types.Count == 0)
        {
            return;
        }

        _context.Expenses.AddRange(
            Expense(cars[0], types[0], 180m, -50, "Vidange + filtres."),
            Expense(cars[1], types[0], 165m, -44, "Vidange."),
            Expense(cars[4], types[3], 640m, -33, "Deux pneus avant."),
            Expense(cars[10], types[5], 1_450m, -12, "Réparation embrayage (immobilisé)."),
            Expense(cars[3], types[4], 25m, -6, "Lavage complet."),
            Expense(cars[8], types[1], 890m, -20, "Assurance trimestrielle."));

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTemplatesAndDocumentsAsync(
        IReadOnlyList<Renting> rentings, CancellationToken cancellationToken)
    {
        // Two templates on purpose. The default needs nothing typed in, so
        // generating a contract just works; the long-stay one asks for a franchise
        // amount, which is what exercises the prompt-then-generate flow.
        var standard = new DocumentTemplate
        {
            Name = "Contrat Carthage — standard",
            Kind = DocumentTemplateKind.Contract,
            Language = Languages.French,
            IsDefault = true,
            IsActive = true,
            BlocksJson = DocumentTemplateBlocks.Serialize(StandardContractBlocks()),
            Fields = new List<DocumentTemplateField>()
        };

        var longStay = new DocumentTemplate
        {
            Name = "Contrat Carthage — longue durée",
            Kind = DocumentTemplateKind.Contract,
            Language = Languages.French,
            IsDefault = false,
            IsActive = true,
            BlocksJson = DocumentTemplateBlocks.Serialize(LongStayContractBlocks()),
            Fields = new List<DocumentTemplateField>
            {
                new()
                {
                    Placeholder = "franchise",
                    Binding = DocumentFieldBinding.AskEachTime,
                    Label = "Franchise applicable (TND)",
                    IsRequired = true
                },
                new()
                {
                    Placeholder = "kilometrageInclus",
                    Binding = DocumentFieldBinding.FixedValue,
                    FixedValue = "3 000 km / mois"
                }
            }
        };

        _context.DocumentTemplates.AddRange(standard, longStay);
        await _context.SaveChangesAsync(cancellationToken);

        // Auto-binding is left to the server-side reconcile for the standard
        // template: every placeholder in it is a known booking field.
        standard.Fields = DocumentTemplateFields.Reconcile(
            DocumentTemplateBlocks.Deserialize(standard.BlocksJson),
            Array.Empty<DocumentTemplateField>(),
            DocumentTemplateKind.Contract);

        longStay.Fields = DocumentTemplateFields.Reconcile(
            DocumentTemplateBlocks.Deserialize(longStay.BlocksJson),
            longStay.Fields!,
            DocumentTemplateKind.Contract);

        await _context.SaveChangesAsync(cancellationToken);

        // Already-issued paperwork, so there are PDFs to open on day one. Each
        // generation needs the write lock the numbering relies on, exactly as the
        // commands do.
        await IssueAsync(() => _documents.GenerateContractAsync(
            new RentalDocumentRequest(rentings[0].Id), cancellationToken), cancellationToken);

        await IssueAsync(() => _documents.GenerateContractAsync(
            new RentalDocumentRequest(rentings[2].Id), cancellationToken), cancellationToken);

        await IssueAsync(() => _documents.GenerateContractAsync(
            new RentalDocumentRequest(
                rentings[6].Id,
                longStay.Id,
                new Dictionary<string, string> { ["franchise"] = "500 TND" }),
            cancellationToken), cancellationToken);

        await IssueAsync(() => _documents.GenerateFactureAsync(
            new RentalDocumentRequest(rentings[2].Id), cancellationToken), cancellationToken);

        await IssueAsync(() => _documents.GenerateFactureAsync(
            new RentalDocumentRequest(rentings[6].Id), cancellationToken), cancellationToken);
    }

    // The document service assigns MAX(sequence)+1 and does not commit; both are
    // the caller's job (see IRentalDocumentService).
    private async Task IssueAsync(Func<Task> generate, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.AcquireTenantWriteLockAsync(cancellationToken);

        await generate();

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- agency 2

    private async Task SeedSaharaAsync(
        Agency agency,
        Country country,
        IReadOnlyDictionary<string, ModelCar> models,
        CancellationToken cancellationToken)
    {
        var branch = Branch("Agence Djerba Midoun", country,
            "Route de Midoun, Djerba", 33.8076, 10.9963);

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        var cars = new[]
        {
            Car("122 TU 4410", models["Picanto"], branch, 65m, "Blanc", 67, FuelType.Gasoline, 2023),
            Car("122 TU 4411", models["i10"], branch, 65m, "Bleu", 67, FuelType.Gasoline, 2023),
            Car("131 TU 7702", models["Symbol"], branch, 78m, "Gris", 75, FuelType.Diesel, 2022),
            Car("145 TU 9915", models["Duster"], branch, 160m, "Blanc", 115, FuelType.Diesel, 2021),
        };

        _context.Cars.AddRange(cars);
        await _context.SaveChangesAsync(cancellationToken);

        var clients = new[]
        {
            Client("Fathi", "Kammoun", 1983, 2, 17, "07112244", null, "04-778811"),
            Client("Salma", "Riahi", 1992, 12, 5, "07334455", null, "12-119933"),
            Client("Ahmed", "Dridi", 1979, 7, 21, "07556677", "K3344551", "03-667722"),
            Client("Olfa", "Mejri", 1995, 3, 30, "07778899", null, "15-882200"),
            Client("Hamza", "Ltaief", 1987, 9, 12, "07990011", null, "09-114466"),
        };

        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync(cancellationToken);

        var rentings = new[]
        {
            Renting(cars[0], clients[0], -18, -14, RentingState.Done, 22_100, 22_640),
            Renting(cars[3], clients[2], -3, +2, RentingState.InProgress, 48_300),
            Renting(cars[1], clients[3], +4, +9, RentingState.NotYet),
        };

        _context.Rentings.AddRange(rentings);
        await _context.SaveChangesAsync(cancellationToken);

        // A single, middling review: the second agency is there to look different
        // from the first, reputation included.
        _context.AgencyReviews.Add(
            Review(agency, rentings[0], clients[0], "Hyundai i10", 3, -13,
                "Correct pour le prix, mais la voiture accusait son kilométrage."));

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------- agencies 3-5: abroad
    //
    // Each of these is deliberately smaller than Carthage: that agency is the
    // "everything switched on" showcase, and these three exist to make the app
    // stop looking like a single-country, single-currency one. What they add is
    // breadth — other currencies, branches in more than one city, and a fleet
    // priced in local money.

    /// <summary>
    /// Morocco, in dirhams. Three branches over two cities, so a search near
    /// Marrakech and one near Casablanca return different fleets from the same
    /// agency.
    /// </summary>
    private async Task SeedAtlasAsync(
        IReadOnlyDictionary<string, ModelCar> models,
        IReadOnlyList<ExtraServicesType> extras,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        var morocco = await CountryAsync("Maroc", cancellationToken);

        var agency = await SeedAgencyAsync(
            "Atlas Rent Casablanca", "45 boulevard d'Anfa, Casablanca",
            "+212 522 27 41 00", "contact@atlasrent.ma", morocco, plan,
            MoroccanDirham, 33.5883, -7.6114, cancellationToken);

        await SeedUserAsync("admin@atlasrent.ma", "Youssef El Fassi", Roles.AgencyAdministrator, agency.Id);

        // Everything below belongs to this agency and is priced in dirhams.
        using var acting = ActAs(agency);

        var anfa = Branch("Agence Casablanca Anfa", morocco,
            "45 boulevard d'Anfa, Casablanca", 33.5883, -7.6114);
        var airport = Branch("Aéroport Mohammed V", morocco,
            "Aéroport Mohammed V, Nouaceur", 33.3675, -7.5898);
        var marrakech = Branch("Agence Marrakech Guéliz", morocco,
            "Avenue Mohammed V, Guéliz, Marrakech", 31.6340, -8.0089);

        _context.Branches.AddRange(anfa, airport, marrakech);
        await _context.SaveChangesAsync(cancellationToken);

        var cars = new[]
        {
            Car("45821-A-6", models["Clio 5"], anfa, 320m, "Blanc", 90, FuelType.Diesel, 2023),
            Car("31207-B-6", models["208"], anfa, 340m, "Gris", 100, FuelType.Gasoline, 2023),
            Car("52940-A-6", models["Duster"], airport, 620m, "Beige", 115, FuelType.Diesel, 2022),
            Car("18663-C-6", models["Logan"], airport, 300m, "Blanc", 90, FuelType.Diesel, 2021),
            Car("77104-A-44", models["Sportage"], marrakech, 780m, "Noir", 136, FuelType.Diesel, 2023),
            Car("20518-B-44", models["Picanto"], marrakech, 260m, "Rouge", 67, FuelType.Gasoline, 2024),
        };

        _context.Cars.AddRange(cars);
        await _context.SaveChangesAsync(cancellationToken);

        var clients = new[]
        {
            Client("Rachid", "Benjelloun", 1984, 5, 9, "BE445120", null, "12/338745", place: "Casablanca"),
            Client("Khadija", "Amrani", 1991, 10, 22, "BK901233", null, "14/220198", place: "Casablanca"),
            Client("Omar", "Tazi", 1977, 2, 14, "BH112907", "MA4471203", "07/889012", place: "Rabat"),
            Client("Latifa", "Ouazzani", 1995, 8, 3, "MC338710", null, "16/447712", place: "Marrakech"),
            Client("Hamid", "Chraibi", 1988, 12, 17, "BJ667401", null, "10/112938", place: "Casablanca"),
            // A European visitor renting on a passport.
            Client("Sofia", "Marchetti", 1990, 6, 28, null, "YB7741209", "MI9930221", place: "Milan"),
        };

        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync(cancellationToken);

        // Windows per car stay disjoint, so the availability rule holds.
        var rentings = new[]
        {
            Renting(cars[0], clients[0], -31, -27, RentingState.Done, 22_400, 23_150),
            Renting(cars[4], clients[2], -24, -19, RentingState.Done, 9_800, 11_240),
            Renting(cars[1], clients[1], -14, -10, RentingState.Done, 31_050, 31_720),
            Renting(cars[2], clients[3], -2, +3, RentingState.InProgress, 47_600),
            Renting(cars[5], clients[4], -1, +4, RentingState.InProgress, 6_320),
            // Car 0 is free again: its earlier booking ended 27 days ago.
            Renting(cars[0], clients[5], +5, +12, RentingState.NotYet,
                notes: "Cliente italienne, permis international vérifié."),
            Renting(cars[3], clients[1], +7, +11, RentingState.NotYet),
        };

        _context.Rentings.AddRange(rentings);
        await _context.SaveChangesAsync(cancellationToken);

        _context.AgencyReviews.AddRange(
            Review(agency, rentings[0], clients[0], "Renault Clio 5", 5, -26,
                "Prise en charge rapide à Anfa, voiture très propre."),
            Review(agency, rentings[1], clients[2], "Kia Sportage", 4, -18,
                "Excellent 4x4 pour l'Atlas. Retour à Marrakech accepté sans frais."),
            Review(agency, rentings[2], clients[1], "Peugeot 208", 3, -9,
                "Voiture correcte mais climatisation faible en plein été."));

        await _context.SaveChangesAsync(cancellationToken);

        // Amounts are given explicitly rather than taken from the type's list
        // price: ExtraServicesType carries a bare number with no currency, and
        // this agency's dirhams are not the dinars that number was written in.
        _context.ExtraServices.AddRange(
            Extra(rentings[1], extras[0], 90m),
            Extra(rentings[1], extras[3], 320m),
            Extra(rentings[3], extras[1], 140m),
            Extra(rentings[5], extras[2], 200m));

        await _context.SaveChangesAsync(cancellationToken);

        // Finished bookings settled in full, live ones part-paid.
        var payments = new List<Payment>();

        for (var index = 0; index < 3; index++)
        {
            payments.Add(Payment(rentings[index], rentings[index].Price!.Amount,
                index == 1 ? PaymentMethod.Transfer : PaymentMethod.Card,
                DaysFromToday(rentings[index].StartDate!.Value)));
        }

        payments.Add(Payment(rentings[3], 900m, PaymentMethod.Cash, -2));
        payments.Add(Payment(rentings[4], 500m, PaymentMethod.Card, -1));

        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync(cancellationToken);

        var pending = Hold(cars[4], clients[3], +14, +19, 48);
        var confirmed = Hold(cars[1], clients[0], +16, +20, 48);
        confirmed.Confirm();

        _context.Reservations.AddRange(pending, confirmed);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// France, in euros — the agency that shows the app working in a currency
    /// and a market with no relation to the Tunisian ones.
    /// </summary>
    private async Task SeedRivieraAsync(
        IReadOnlyDictionary<string, ModelCar> models,
        IReadOnlyList<ExtraServicesType> extras,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        var france = await CountryAsync("France", cancellationToken);

        var agency = await SeedAgencyAsync(
            "Riviera Location Nice", "18 promenade des Anglais, Nice",
            "+33 4 93 87 12 40", "contact@riviera-location.fr", france, plan,
            Euro, 43.6952, 7.2650, cancellationToken);

        await SeedUserAsync("admin@riviera.fr", "Camille Rousseau", Roles.AgencyAdministrator, agency.Id);

        using var acting = ActAs(agency);

        var promenade = Branch("Agence Nice Promenade", france,
            "18 promenade des Anglais, Nice", 43.6952, 7.2650);
        var airport = Branch("Aéroport Nice Côte d'Azur", france,
            "Terminal 2, Aéroport Nice Côte d'Azur, Nice", 43.6584, 7.2159);
        var cannes = Branch("Agence Cannes Gare", france,
            "1 place de la Gare, Cannes", 43.5528, 7.0174);

        _context.Branches.AddRange(promenade, airport, cannes);
        await _context.SaveChangesAsync(cancellationToken);

        var cars = new[]
        {
            Car("AB-274-CD", models["Clio 5"], promenade, 52m, "Gris", 90, FuelType.Diesel, 2023),
            Car("BF-118-KL", models["Polo"], promenade, 55m, "Blanc", 95, FuelType.Gasoline, 2024),
            Car("CG-903-RT", models["Golf 8"], airport, 84m, "Noir", 130, FuelType.Diesel, 2023),
            Car("DH-556-MN", models["308"], airport, 68m, "Bleu", 130, FuelType.Diesel, 2022),
            Car("EJ-741-PQ", models["Tucson"], cannes, 118m, "Blanc", 136, FuelType.Diesel, 2023),
            Car("FK-320-ST", models["Corolla"], cannes, 72m, "Argent", 122, FuelType.Gasoline, 2024),
        };

        _context.Cars.AddRange(cars);
        await _context.SaveChangesAsync(cancellationToken);

        var clients = new[]
        {
            Client("Julien", "Moreau", 1986, 3, 11, null, "19FR88201", "060411223", place: "Nice"),
            Client("Élodie", "Girard", 1993, 7, 26, null, "20FR11947", "930722118", place: "Cannes"),
            Client("Thomas", "Lambert", 1979, 11, 4, null, "17FR55103", "790112004", place: "Lyon"),
            Client("Chloé", "Petit", 1997, 1, 19, null, "21FR33472", "970119338", place: "Nice"),
            // A British visitor: the passport-only path again, in another market.
            Client("Daniel", "Whitfield", 1982, 9, 8, null, "GB4471902", "WHITF802091", place: "Londres"),
        };

        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync(cancellationToken);

        var rentings = new[]
        {
            Renting(cars[2], clients[0], -27, -22, RentingState.Done, 34_900, 36_180),
            Renting(cars[0], clients[1], -19, -16, RentingState.Done, 21_400, 21_910),
            Renting(cars[4], clients[4], -12, -6, RentingState.Done, 15_200, 17_050,
                notes: "Client britannique, retour à Cannes."),
            Renting(cars[1], clients[2], -3, +2, RentingState.InProgress, 8_760),
            Renting(cars[5], clients[3], 0, +7, RentingState.InProgress, 4_120),
            Renting(cars[2], clients[1], +6, +10, RentingState.NotYet),
            Renting(cars[3], clients[0], +8, +15, RentingState.NotYet,
                notes: "Livraison au terminal 2, vol AF 1006.", secondDriver: clients[1]),
        };

        _context.Rentings.AddRange(rentings);
        await _context.SaveChangesAsync(cancellationToken);

        _context.AgencyReviews.AddRange(
            Review(agency, rentings[0], clients[0], "Volkswagen Golf 8", 5, -21,
                "Voiture neuve, comptoir à deux pas de l'hôtel. Parfait."),
            Review(agency, rentings[1], clients[1], "Renault Clio 5", 4, -15,
                "Tout s'est bien passé, mais le plein était à faire au départ."),
            Review(agency, rentings[2], clients[4], "Hyundai Tucson", 5, -5, comment: null));

        await _context.SaveChangesAsync(cancellationToken);

        _context.ExtraServices.AddRange(
            Extra(rentings[0], extras[3], 28m),
            Extra(rentings[2], extras[0], 9m),
            Extra(rentings[2], extras[1], 14m),
            Extra(rentings[6], extras[2], 22m));

        await _context.SaveChangesAsync(cancellationToken);

        var payments = new List<Payment>();

        for (var index = 0; index < 3; index++)
        {
            payments.Add(Payment(rentings[index], rentings[index].Price!.Amount,
                PaymentMethod.Card, DaysFromToday(rentings[index].StartDate!.Value)));
        }

        payments.Add(Payment(rentings[3], 150m, PaymentMethod.Card, -3));
        payments.Add(Payment(rentings[6], 200m, PaymentMethod.Transfer, 0, "Acompte à la réservation."));

        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync(cancellationToken);

        var pending = Hold(cars[4], clients[2], +12, +17, 48);
        var paid = Hold(cars[0], clients[3], +14, +18, 48);
        paid.Confirm();
        paid.MarkPaid();

        _context.Reservations.AddRange(pending, paid);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The United Arab Emirates, in dirhams, on the Starter plan — so this one
    /// deliberately has no payments, extras or paperwork: those modules are off,
    /// and seeding data its own screens cannot show would be misleading.
    /// Its Arabic-market names also give the RTL layout something real to render.
    /// </summary>
    private async Task SeedGulfDriveAsync(
        IReadOnlyDictionary<string, ModelCar> models,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        var emirates = await CountryAsync("Émirats arabes unis", cancellationToken);

        var agency = await SeedAgencyAsync(
            "Gulf Drive Dubaï", "Sheikh Zayed Road, Business Bay, Dubaï",
            "+971 4 321 88 00", "contact@gulfdrive.ae", emirates, plan,
            UaeDirham, 25.1857, 55.2664, cancellationToken);

        await SeedUserAsync("admin@gulfdrive.ae", "Omar Al Marzooqi", Roles.AgencyAdministrator, agency.Id);

        using var acting = ActAs(agency);

        var businessBay = Branch("Agence Business Bay", emirates,
            "Sheikh Zayed Road, Business Bay, Dubaï", 25.1857, 55.2664);
        var airport = Branch("Aéroport international de Dubaï", emirates,
            "Terminal 3, Aéroport international de Dubaï", 25.2532, 55.3657);

        _context.Branches.AddRange(businessBay, airport);
        await _context.SaveChangesAsync(cancellationToken);

        var cars = new[]
        {
            Car("N 41287", models["Corolla"], businessBay, 240m, "Blanc", 122, FuelType.Gasoline, 2024),
            Car("K 60934", models["Sportage"], businessBay, 420m, "Gris", 136, FuelType.Gasoline, 2023),
            Car("P 15502", models["Yaris"], airport, 190m, "Argent", 100, FuelType.Gasoline, 2023),
            Car("Q 88710", models["Tucson"], airport, 460m, "Noir", 136, FuelType.Gasoline, 2024),
        };

        _context.Cars.AddRange(cars);
        await _context.SaveChangesAsync(cancellationToken);

        var clients = new[]
        {
            Client("Ahmed", "Al Suwaidi", 1985, 4, 16, "784198512345", null, "DXB4471203", place: "Dubaï"),
            Client("Fatima", "Al Hashimi", 1992, 11, 2, "784199223317", null, "DXB9930118", place: "Charjah"),
            Client("Rajesh", "Menon", 1980, 6, 25, null, "IN7741023", "KL1102934", place: "Kochi"),
            Client("Sarah", "Kensington", 1989, 2, 8, null, "GB9920471", "KENSI890208", place: "Manchester"),
        };

        _context.Clients.AddRange(clients);
        await _context.SaveChangesAsync(cancellationToken);

        var rentings = new[]
        {
            Renting(cars[0], clients[0], -21, -17, RentingState.Done, 12_400, 13_050),
            Renting(cars[3], clients[2], -9, -4, RentingState.Done, 5_100, 6_720),
            Renting(cars[1], clients[1], -1, +4, RentingState.InProgress, 28_900),
            Renting(cars[2], clients[3], +3, +8, RentingState.NotYet),
        };

        _context.Rentings.AddRange(rentings);
        await _context.SaveChangesAsync(cancellationToken);

        _context.AgencyReviews.AddRange(
            Review(agency, rentings[0], clients[0], "Toyota Corolla", 5, -16,
                "خدمة سريعة والسيارة نظيفة تماما."),
            Review(agency, rentings[1], clients[2], "Hyundai Tucson", 4, -3,
                "Smooth pick-up at Terminal 3, would rent again."));

        await _context.SaveChangesAsync(cancellationToken);

        var pending = Hold(cars[3], clients[1], +10, +14, 48);

        _context.Reservations.Add(pending);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------- shared reference data

    private async Task<IReadOnlyDictionary<string, ModelCar>> SeedCarCatalogAsync(
        CancellationToken cancellationToken)
    {
        var catalog = new Dictionary<string, string[]>
        {
            ["Renault"] = new[] { "Clio 5", "Symbol", "Megane" },
            ["Volkswagen"] = new[] { "Polo", "Golf 8" },
            ["Peugeot"] = new[] { "208", "308" },
            ["Kia"] = new[] { "Picanto", "Sportage" },
            ["Hyundai"] = new[] { "i10", "Tucson" },
            ["Toyota"] = new[] { "Yaris", "Corolla" },
            ["Dacia"] = new[] { "Logan", "Duster" },
        };

        var models = new Dictionary<string, ModelCar>(StringComparer.Ordinal);

        foreach (var (brandName, modelNames) in catalog)
        {
            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == brandName, cancellationToken);

            if (brand is null)
            {
                brand = new Brand { Name = brandName };
                _context.Brands.Add(brand);
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var modelName in modelNames)
            {
                var model = await _context.ModelCars
                    .FirstOrDefaultAsync(m => m.Name == modelName && m.BrandId == brand.Id, cancellationToken);

                if (model is null)
                {
                    model = new ModelCar { Name = modelName, BrandId = brand.Id };
                    _context.ModelCars.Add(model);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                models[modelName] = model;
            }
        }

        return models;
    }

    private async Task<IReadOnlyList<ExtraServicesType>> SeedExtraServiceTypesAsync(
        CancellationToken cancellationToken)
    {
        var wanted = new (string Name, decimal Amount)[]
        {
            ("GPS", 25m),
            ("Siège bébé", 40m),
            ("Conducteur additionnel", 60m),
            ("Assurance tous risques", 90m),
            ("Wifi portable", 30m),
        };

        var types = new List<ExtraServicesType>();

        foreach (var (name, amount) in wanted)
        {
            var type = await _context.ExtraServicesTypes.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

            if (type is null)
            {
                type = new ExtraServicesType { Name = name, Amount = amount, IsActive = true };
                _context.ExtraServicesTypes.Add(type);
                await _context.SaveChangesAsync(cancellationToken);
            }

            types.Add(type);
        }

        return types;
    }

    private async Task SeedExpenseTypesAsync(CancellationToken cancellationToken)
    {
        var wanted = new (string Name, bool Notify, int? Km, int? Months)[]
        {
            ("Vidange", true, 10_000, 12),
            ("Assurance", true, null, 3),
            ("Vignette", true, null, 12),
            ("Pneus", true, 40_000, null),
            ("Lavage", false, null, null),
            ("Réparation", false, null, null),
        };

        foreach (var (name, notify, km, months) in wanted)
        {
            if (await _context.ExpenseTypes.AnyAsync(t => t.Name == name, cancellationToken))
            {
                continue;
            }

            _context.ExpenseTypes.Add(new ExpenseType
            {
                Name = name,
                IsActive = true,
                WithNotif = notify,
                AfterKilometer = km,
                AfterMonth = months
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Acts as one agency for everything written inside the scope: the ambient
    /// tenant (so AgencyId is stamped and the query filters match) and the
    /// currency every Money is written in. Both are restored on dispose, so
    /// agencies cannot bleed into each other.
    /// </summary>
    private IDisposable ActAs(Agency agency)
    {
        var previousCurrency = _currency;
        var tenant = AmbientTenant.Push(agency.Id);

        _currency = agency.Settings?.CurrencyCode ?? previousCurrency;

        return new Acting(tenant, () => _currency = previousCurrency);
    }

    private sealed class Acting : IDisposable
    {
        private readonly IDisposable _tenant;
        private readonly Action _restore;

        public Acting(IDisposable tenant, Action restore)
        {
            _tenant = tenant;
            _restore = restore;
        }

        public void Dispose()
        {
            _restore();
            _tenant.Dispose();
        }
    }

    /// <summary>
    /// A branch with its street address and its pin — the two halves the map
    /// picker fills in together. Latitude first, as it is written and said;
    /// GeoPoint does the swap into the Point's (X=longitude, Y=latitude) order.
    /// </summary>
    private static Branch Branch(string name, Country country, string address, double latitude, double longitude)
        => new()
        {
            Name = name,
            CountryId = country.Id,
            Address = address,
            Location = GeoPoint.ToPoint(latitude, longitude)
        };

    private async Task<Country> CountryAsync(string name, CancellationToken cancellationToken)
    {
        var country = await _context.Countries.FirstOrDefaultAsync(c => c.Name == name, cancellationToken);

        if (country is null)
        {
            country = new Country { Name = name };
            _context.Countries.Add(country);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return country;
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in new[]
                 {
                     Roles.PlatformAdministrator, Roles.AgencyAdministrator,
                     Roles.AgencyStaff, Roles.Customer
                 })
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    /// <summary>
    /// The named plan, created if the database does not have it. The two plans are
    /// the point of the second agency: Starter withholds Payments, ExtraServices,
    /// Contracts and Factures, so the feature gate is visible by logging in.
    /// </summary>
    private async Task<SubscriptionPlan> PlanAsync(string name, CancellationToken cancellationToken)
    {
        var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

        if (plan is not null)
        {
            return plan;
        }

        var features = name == "Starter"
            ? new[]
            {
                FeatureFlags.Cars, FeatureFlags.Clients, FeatureFlags.Branches,
                FeatureFlags.Rentings, FeatureFlags.Reservations,
            }
            : FeatureFlags.All;

        plan = new SubscriptionPlan
        {
            Name = name,
            MaxCars = name == "Starter" ? 10 : 1_000,
            MaxClients = name == "Starter" ? 50 : 5_000,
            MaxUsers = name == "Starter" ? 3 : 100,
            Price = name == "Starter" ? 0m : 299m,
            Features = features.Select(f => new PlanFeature { Feature = f }).ToList()
        };

        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);

        return plan;
    }

    private async Task<Agency> SeedAgencyAsync(
        string name, string address, string phone, string email,
        Country country, SubscriptionPlan plan, string currency,
        double latitude, double longitude, CancellationToken cancellationToken)
    {
        var now = _dateTime.GetUtcNow();

        var agency = new Agency
        {
            Name = name,
            Address = address,
            PhoneNumber = phone,
            Email = email,
            CountryId = country.Id,
            // The head-office pin, as the agency form's map picker would have set
            // it. Coordinates are given latitude-first here and swapped once, in
            // GeoPoint, rather than at each call site.
            Location = GeoPoint.ToPoint(latitude, longitude),
            Settings = new AgencySettings
            {
                CurrencyCode = currency,
                CancellationWindowHours = 24,
                ReservationExpiryHours = 48
            }
        };

        _context.Agencies.Add(agency);
        await _context.SaveChangesAsync(cancellationToken);

        // Written before any tenant data: an agency without an active subscription
        // cannot be written to at all (SubscriptionEnforcementInterceptor).
        _context.AgencySubscriptions.Add(new AgencySubscription
        {
            AgencyId = agency.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartDate = now.AddMonths(-6),
            EndDate = now.AddYears(2)
        });

        await _context.SaveChangesAsync(cancellationToken);

        return agency;
    }

    private async Task SeedUserAsync(
        string email, string fullName, string role, int? agencyId, string[]? permissions = null)
    {
        var user = await _userManager.FindByNameAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                AgencyId = agencyId,
                PreferredLanguage = Languages.French
            };

            var result = await _userManager.CreateAsync(user, DemoPassword);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Could not create demo user {Email}: {Errors}",
                    email, string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        if (!await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        // Administrators hold every permission by role, so grants are only written
        // for staff.
        foreach (var permission in permissions ?? Array.Empty<string>())
        {
            if (!await _context.UserPermissions.AnyAsync(p => p.UserId == user.Id && p.Permission == permission))
            {
                _context.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = permission });
            }
        }

        await _context.SaveChangesAsync();
    }

    private Car Car(
        string matricule, ModelCar model, Branch branch, decimal dailyRate,
        string colour, int power, FuelType fuel, int firstCirculationYear,
        CarStatus status = CarStatus.Active) => new()
        {
            Matricule = matricule,
            ModelId = model.Id,
            BranchId = branch.Id,
            Status = status,
            DailyRate = Money.Of(dailyRate, _currency),
            Color = colour,
            Power = power,
            FuelType = fuel,
            FirstCirculationDate = new DateTime(firstCirculationYear, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    // `place` is where the client and their papers come from. It defaults to Tunis
    // for the two Tunisian agencies, which were the only ones here first; the
    // international agencies pass their own city so a Nice client is not recorded
    // as holding a Tunisian ID card.
    private static Client Client(
        string firstName, string lastName, int birthYear, int birthMonth, int birthDay,
        string? cin, string? passport, string? licence,
        bool flagged = false, string? notes = null, string place = "Tunis") => new()
        {
            FirstName = firstName,
            LastName = lastName,
            BirthDate = new DateTime(birthYear, birthMonth, birthDay, 0, 0, 0, DateTimeKind.Utc),
            BirthPlace = place,
            CIN = cin,
            CINDeliveranceDate = cin is null
                ? null
                : new DateTime(birthYear + 18, birthMonth, birthDay, 0, 0, 0, DateTimeKind.Utc),
            CINDeliverancePlace = cin is null ? null : place,
            PasseportNumber = passport,
            DrivingLicenceNumber = licence,
            DrivingLicenceDeliveranceDate = licence is null
                ? null
                : new DateTime(birthYear + 20, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            DrivingLicenceDeliverancePlace = licence is null ? null : place,
            IsFlagged = flagged,
            Notes = notes
        };

    private Renting Renting(
        Car car, Client client, int startOffsetDays, int endOffsetDays, RentingState state,
        int? startMileage = null, int? endMileage = null,
        string? notes = null, Client? secondDriver = null)
    {
        var start = Today.AddDays(startOffsetDays);
        var end = Today.AddDays(endOffsetDays);

        return new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            SecondClientId = secondDriver?.Id,
            StartDate = start,
            EndDate = end,
            StartMileage = startMileage,
            EndMileage = endMileage,
            // From the pricing seam rather than a second copy of the arithmetic, so
            // seeded prices match what the app would have quoted.
            Price = _pricing.CalculateRentalPrice(car, start, end),
            DepositAmount = Money.Of(300m, _currency),
            RentingState = state,
            Notes = notes
        };
    }

    // A published rating. AgencyReview is platform-level shop-window content
    // rather than tenant data, so the agency is written explicitly here instead
    // of being stamped from the ambient tenant like everything around it. The
    // author's name and the car's are snapshots, which is what lets the public
    // shopfront render a review without reading tenant-filtered tables.
    private AgencyReview Review(
        Agency agency, Renting renting, Client client, string carName,
        int rating, int daysAgo, string? comment)
        => new()
        {
            AgencyId = agency.Id,
            RentingId = renting.Id,
            ClientId = client.Id,
            AuthorName = $"{client.FirstName} {client.LastName}",
            CarName = carName,
            Rating = rating,
            Comment = comment,
            SubmittedAt = Today.AddDays(daysAgo),
        };

    private Reservation Hold(
        Car car, Client client, int startOffsetDays, int endOffsetDays, int expiryOffsetHours)
    {
        var start = Today.AddDays(startOffsetDays);
        var end = Today.AddDays(endOffsetDays);

        return Reservation.Create(
            car.Id,
            start,
            end,
            _pricing.CalculateRentalPrice(car, start, end),
            _dateTime.GetUtcNow().UtcDateTime.AddHours(expiryOffsetHours),
            client.Id,
            depositAmount: Money.Of(200m, _currency));
    }

    private ExtraService Extra(Renting renting, ExtraServicesType type, decimal? overrideAmount = null) => new()
    {
        RentingId = renting.Id,
        ExtraServicesTypeId = type.Id,
        TotalAmount = Money.Of(overrideAmount ?? type.Amount ?? 0m, _currency)
    };

    private Payment Payment(
        Renting renting, decimal amount, PaymentMethod method, int dayOffset, string? notes = null) => new()
        {
            ClientId = renting.ClientId,
            RentingId = renting.Id,
            PayementDate = Today.AddDays(dayOffset),
            PayementAmount = Money.Of(amount, _currency),
            Method = method,
            Notes = notes
        };

    private Expense Expense(Car car, ExpenseType type, decimal amount, int dayOffset, string? notes) => new()
    {
        CarId = car.Id,
        ExpenseTypeId = type.Id,
        ExpenseDate = Today.AddDays(dayOffset),
        ExpenseAmount = Money.Of(amount, _currency),
        Description = notes
    };

    private int DaysFromToday(DateTime date) => (int)(date.Date - Today).TotalDays;

    // The default contract layout, as blocks. Mirrors the shipped example's shape
    // but in the agency's own words, so editing it in the UI shows a real template
    // rather than a copy of the fallback.
    private static List<DocumentBlock> StandardContractBlocks() => new()
    {
        new DocumentBlock { Type = DocumentBlockType.Heading, Text = "CONTRAT DE LOCATION" },
        new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Fine = true,
            Text = "N° {{document.number}} — Établi le {{document.issuedAt}}"
        },
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            SideBySide = true,
            Title = "Loueur",
            Fields = new List<DocumentBlockField>
            {
                new() { Value = "{{agency.name}}" },
                new() { Value = "{{agency.address}}", HideWhenEmpty = true },
                new() { Value = "{{agency.phoneNumber}}", HideWhenEmpty = true },
            }
        },
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            SideBySide = true,
            Title = "Locataire",
            Fields = new List<DocumentBlockField>
            {
                new() { Value = "{{client.fullName}}" },
                new() { Label = "Né(e) le", Value = "{{client.birthDate}}", HideWhenEmpty = true },
                new() { Label = "CIN", Value = "{{client.cin}}", HideWhenEmpty = true },
                new() { Label = "Passeport", Value = "{{client.passeportNumber}}", HideWhenEmpty = true },
                new() { Label = "Permis", Value = "{{client.drivingLicenceNumber}}", HideWhenEmpty = true },
            }
        },
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = "Véhicule",
            Fields = new List<DocumentBlockField>
            {
                new() { Label = "Modèle", Value = "{{car.model}}", HideWhenEmpty = true },
                new() { Label = "Immatriculation", Value = "{{car.matricule}}", HideWhenEmpty = true },
                new() { Label = "Couleur", Value = "{{car.color}}", HideWhenEmpty = true },
                new() { Label = "Carburant", Value = "{{car.fuelType}}", HideWhenEmpty = true },
            }
        },
        new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = "Période et tarif",
            Fields = new List<DocumentBlockField>
            {
                new() { Label = "Du", Value = "{{renting.startDate}}" },
                new() { Label = "Au", Value = "{{renting.endDate}}" },
                new() { Label = "Durée", Value = "{{renting.days}} jour(s)" },
                new() { Label = "Kilométrage au départ", Value = "{{renting.startMileage}}", HideWhenEmpty = true },
                new() { Label = "Montant", Value = "{{renting.price}}" },
                new() { Label = "Caution", Value = "{{renting.deposit}}", HideWhenEmpty = true },
            }
        },
        new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Fine = true,
            Text = "Le locataire reconnaît avoir reçu le véhicule décrit ci-dessus en bon état de "
                 + "fonctionnement et s'engage à le restituer à la date convenue, dans le même état et avec "
                 + "le même niveau de carburant. Le locataire demeure responsable des amendes et dommages "
                 + "survenus pendant la période de location."
        },
        new DocumentBlock
        {
            Type = DocumentBlockType.Signatures,
            Labels = new List<string> { "Signature du loueur", "Signature du locataire" }
        },
    };

    // The long-stay variant: same spine, plus the two placeholders that are not in
    // anybody's database column — one asked for each time, one fixed on the template.
    private static List<DocumentBlock> LongStayContractBlocks()
    {
        var blocks = StandardContractBlocks();

        blocks.Insert(blocks.Count - 1, new DocumentBlock
        {
            Type = DocumentBlockType.Fields,
            Title = "Conditions longue durée",
            Fields = new List<DocumentBlockField>
            {
                new() { Label = "Franchise", Value = "{{franchise}}" },
                new() { Label = "Kilométrage inclus", Value = "{{kilometrageInclus}}" },
                new() { Label = "Lieu de restitution", Value = string.Empty },
            }
        });

        return blocks;
    }
}
