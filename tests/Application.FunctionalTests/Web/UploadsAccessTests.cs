using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.SubmitMyReservationRequirementCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationRequirementsQuery;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.SetReservationRequirementsCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Application.FunctionalTests.Web;

using static Testing;

/// <summary>
/// The gate in front of the uploaded files. Before it, every file was a static
/// file anyone could fetch — and an issued contract's path is its number, so the
/// whole archive could be read by counting.
/// </summary>
public class UploadsAccessTests : BaseTestFixture
{
    [Test]
    public async Task AnIssuedContractCannotBeFetchedByCountingItsNumber()
    {
        var url = WriteUpload("agencies/1/contracts/CTR-2030-000001.pdf");

        var response = await CreateHttpClient().GetAsync(url);

        // 404, not 403: a refusal would confirm the number exists.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ACarPhotoStaysPublicForTheMarketplace()
    {
        var url = WriteUpload("agencies/1/cars/3/photo-abc.jpg");

        var response = await CreateHttpClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    [Test]
    public async Task AMissingUploadIsNotAnsweredWithTheSpa()
    {
        var response = await CreateHttpClient().GetAsync("/uploads/agencies/1/cars/3/nothing.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AnAgencyUserOpensTheirOwnAgencysFilesAndNoOneElses()
    {
        var userId = await RunAsAgencyStaffAsync();
        var agencyId = await AddTestAgencyAsync();
        await AttachToAgencyAsync(userId, agencyId);

        var mine = WriteUpload($"agencies/{agencyId}/clients/7/cin-abc.jpg");
        var theirs = WriteUpload($"agencies/{agencyId + 1000}/clients/7/cin-abc.jpg");

        var client = await SignedInClientAsync(userId);

        var own = await client.GetAsync(mine);
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        // Nobody's passport left behind in a shared front-desk browser cache.
        own.Headers.CacheControl!.NoStore.Should().BeTrue();

        (await client.GetAsync(theirs)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task APlatformAdministratorsLookIsOnTheAuditTrail()
    {
        var adminId = await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        var url = WriteUpload($"agencies/{agencyId}/factures/FAC-2030-000001.pdf");

        var response = await (await SignedInClientAsync(adminId)).GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var row = (await AllIgnoringFiltersAsync<AuditLog>())
            .Single(a => a.Action == ImpersonationAuditor.ImpersonatedReadAction);
        row.AgencyId.Should().Be(agencyId);
        row.After.Should().Contain("FAC-2030-000001.pdf");
    }

    /// <summary>
    /// A PDF viewer reads one document in several range requests; that is one
    /// look, not five. A different file is a different look.
    /// </summary>
    [Test]
    public async Task OneOpeningIsOneAuditRowHoweverManyRequestsItTakes()
    {
        var adminId = await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        var invoice = WriteUpload($"agencies/{agencyId}/factures/FAC-2030-000002.pdf");
        var contract = WriteUpload($"agencies/{agencyId}/contracts/CTR-2030-000002.pdf");

        var client = await SignedInClientAsync(adminId);
        for (var i = 0; i < 3; i++)
            (await client.GetAsync(invoice)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync(contract)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await AllIgnoringFiltersAsync<AuditLog>())
            .Where(a => a.Action == ImpersonationAuditor.ImpersonatedReadAction)
            .OrderBy(a => a.Id)
            .Select(a => a.After)
            .Should().SatisfyRespectively(
                first => first.Should().Contain("FAC-2030-000002.pdf"),
                second => second.Should().Contain("CTR-2030-000002.pdf"));
    }

    [Test]
    public async Task ACustomerOpensWhatTheySentInAndNothingElse()
    {
        var (customerId, agencyId, reservationId, askId) = await ACustomerAskedForADocumentAsync();

        await SendAsync(new SubmitMyReservationRequirementCommand
        {
            Id = askId,
            FileName = "licence.pdf",
            ContentType = "application/pdf",
            Content = new MemoryStream(PdfBytes),
            Length = PdfBytes.Length
        });

        var sent = (await SendAsync(new GetMyReservationRequirementsQuery(reservationId))).Single().SubmittedFileUrl;
        sent.Should().NotBeNull();

        // Another file of the same agency, which this customer has no claim on.
        var other = WriteUpload($"agencies/{agencyId}/clients/1/cin-xyz.jpg");

        var client = await SignedInClientAsync(customerId);
        (await client.GetAsync(sent)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync(other)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A doubled slash still names the same file.
        (await client.GetAsync(sent!.Replace("/agencies/", "/agencies//"))).StatusCode.Should().Be(HttpStatusCode.OK);

        // And another customer gets nothing of it.
        var strangerId = await RunAsUserAsync("stranger@local", "Customer1234!", new[] { Roles.Customer });
        (await (await SignedInClientAsync(strangerId)).GetAsync(sent)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The file comes from outside the agency and is opened by its staff on our
    /// own origin, so a type that can carry script must never get in.
    /// </summary>
    [TestCase("licence.svg", "image/svg+xml")]
    [TestCase("licence.html", "text/html")]
    [TestCase("licence.js", "application/javascript")]
    public async Task ACustomerCannotSendInAFileThatCarriesScript(string fileName, string contentType)
    {
        var (_, _, _, askId) = await ACustomerAskedForADocumentAsync();

        await FluentActions.Invoking(() => SendAsync(new SubmitMyReservationRequirementCommand
        {
            Id = askId,
            FileName = fileName,
            ContentType = contentType,
            Content = new MemoryStream(PdfBytes),
            Length = PdfBytes.Length
        })).Should().ThrowAsync<ValidationException>();
    }

    /// <summary>Whatever is already on disk, only the upload types are ever served.</summary>
    [Test]
    public async Task AFileOfAnotherTypeIsNotServedEvenToItsAgency()
    {
        var userId = await RunAsAgencyStaffAsync();
        var agencyId = await AddTestAgencyAsync();
        await AttachToAgencyAsync(userId, agencyId);

        var svg = WriteUpload($"agencies/{agencyId}/reservations/1/requirement-1-abc.svg");
        var photo = WriteUpload($"agencies/{agencyId}/cars/1/photo-abc.svg");

        (await (await SignedInClientAsync(userId)).GetAsync(svg)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CreateHttpClient().GetAsync(photo)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The gate reads the path on '/', but the disk also splits on '\' (which
    /// Kestrel decodes from %5C) and climbs on '..'. A path dressed as a public
    /// car photo must not reach another agency's passport scan.
    /// </summary>
    [TestCase("agencies/1/cars/x/..%5C..%5C..%5C2%5Cclients%5C7%5Ccin-abc.jpg")]
    [TestCase("agencies/1/cars/x/..\\..\\..\\2\\clients\\7\\cin-abc.jpg")]
    [TestCase("agencies/1/cars/x/%2e%2e/%2e%2e/%2e%2e/2/clients/7/cin-abc.jpg")]
    [TestCase("agencies/1/cars./../2/clients/7/cin-abc.jpg")]
    public async Task APathDressedAsACarPhotoCannotReachAnotherAgencysFile(string path)
    {
        WriteUpload("agencies/2/clients/7/cin-abc.jpg");
        WriteUpload("agencies/1/cars/x/photo.jpg");

        var response = await CreateHttpClient().GetAsync("/uploads/" + path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Only /api used to be closed to an account on a mailed password, and the
    /// contracts' paths can be counted — so the files must be closed too.
    /// </summary>
    [Test]
    public async Task AnAccountStillOnItsTemporaryPasswordOpensNothing()
    {
        var userId = await RunAsAgencyStaffAsync();
        var agencyId = await AddTestAgencyAsync();
        await AttachToAgencyAsync(userId, agencyId, mustChangePassword: true);

        var contract = WriteUpload($"agencies/{agencyId}/contracts/CTR-2030-000001.pdf");

        (await (await SignedInClientAsync(userId)).GetAsync(contract)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task NothingIsAuditedForAFileThatIsNotThere()
    {
        var adminId = await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var response = await (await SignedInClientAsync(adminId))
            .GetAsync($"/uploads/agencies/{agencyId}/factures/FAC-2030-999999.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AllIgnoringFiltersAsync<AuditLog>()).Should().BeEmpty();
    }

    /// <summary>
    /// Identical bytes are stored once — but only within one area, because the
    /// area decides who may open the file. A car photo that matched a payment
    /// proof would otherwise inherit its private path and vanish from the
    /// marketplace.
    /// </summary>
    [Test]
    public async Task IdenticalBytesAreSharedWithinAnAreaButNotAcrossOne()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var proof = await StoreAsync($"agencies/{agencyId}/payments/1/proof-a.png");
        var photo = await StoreAsync($"agencies/{agencyId}/cars/1/photo-a.png");
        var secondProof = await StoreAsync($"agencies/{agencyId}/payments/2/proof-b.png");

        photo.Should().StartWith($"/uploads/agencies/{agencyId}/cars/");
        secondProof.Should().Be(proof);
    }

    /// <summary>
    /// Car photos are not rate-limited, so a marketplace page can show dozens.
    /// That exemption must hold only for a photo that exists, or naming made-up
    /// car paths would be a way round the limiter.
    /// </summary>
    [Test]
    public async Task OnlyACarPhotoThatExistsEscapesTheRateLimit()
    {
        var photo = WriteUpload("agencies/1/cars/3/photo-limit.jpg");

        using var factory = WithSetting("RateLimiting:AnonymousPerMinute", "2");
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        for (var i = 0; i < 5; i++)
            (await client.GetAsync(photo)).StatusCode.Should().Be(HttpStatusCode.OK);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await client.GetAsync($"/uploads/agencies/1/cars/3/made-up-{i}.jpg")).StatusCode);

        statuses.Should().Equal(HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// A client list of portraits must not spend the budget the screen's own API
    /// calls need: opened files are counted on their own.
    /// </summary>
    [Test]
    public async Task OpeningFilesDoesNotSpendTheApiBudget()
    {
        var userId = await RunAsAgencyStaffAsync();
        var agencyId = await AddTestAgencyAsync();
        await AttachToAgencyAsync(userId, agencyId);
        var portrait = WriteUpload($"agencies/{agencyId}/clients/7/portrait-limit.jpg");

        using var factory = WithSetting("RateLimiting:AuthenticatedUploadsPerMinute", "2");
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Authorization = (await SignedInClientAsync(userId)).DefaultRequestHeaders.Authorization;

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await client.GetAsync(portrait)).StatusCode);

        statuses.Should().Equal(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
        (await client.GetAsync("/Identity/Account/Login")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// A file left on disk by an agency that no longer exists is refused, not
    /// written to the trail against an agency nobody can find.
    /// </summary>
    [Test]
    public async Task AFileOfAnAgencyThatNoLongerExistsIsNotAudited()
    {
        var adminId = await RunAsPlatformAdministratorAsync();
        var orphan = WriteUpload("agencies/987654/factures/FAC-2030-000003.pdf");

        var response = await (await SignedInClientAsync(adminId)).GetAsync(orphan);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AllIgnoringFiltersAsync<AuditLog>()).Should().BeEmpty();
    }

    [Test]
    public void AnEmptyUploadsPrefixIsRefusedAtStartup()
    {
        using var factory = WithSetting("FileStorage:PublicBasePath", "/");

        FluentActions.Invoking(() => factory.CreateClient())
            .Should().Throw<Exception>()
            .Where(e => e.ToString().Contains("PublicBasePath"));
    }

    [Test]
    public async Task APageCarriesTheContentSecurityPolicy()
    {
        var response = await CreateHttpClient().GetAsync("/Identity/Account/Login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var policy = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        policy.Should().Contain("script-src 'self'").And.Contain("frame-ancestors 'none'");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");

        // The page's own scripts moved out of line for that policy; they must be there.
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("/Identity/js/auth-theme.js").And.NotContain("<script>");
        (await CreateHttpClient().GetAsync("/Identity/js/auth-theme.js")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string WriteUpload(string relativePath)
    {
        var fullPath = Path.Combine(UploadsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [1, 2, 3]);

        return "/uploads/" + relativePath;
    }

    private static readonly byte[] PdfBytes = "%PDF-1.4 licence"u8.ToArray();

    /// <summary>
    /// A confirmed booking whose agency asked the customer for a document; ends
    /// signed in as that customer.
    /// </summary>
    private static async Task<(string CustomerId, int AgencyId, int ReservationId, int AskId)> ACustomerAskedForADocumentAsync()
    {
        var customerId = await RunAsUserAsync("upload-cust@local", "Customer1234!", new[] { Roles.Customer });
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "UPL-1", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);
        SetCurrentAgency(null);

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id,
            StartDate = new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2030, 7, 4, 0, 0, 0, DateTimeKind.Utc),
            FirstName = "Jane", LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await RunAsAgencyAdministratorAsync();
        var agencyId = (await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId))!.AgencyId;
        SetCurrentAgency(agencyId);
        await SendAsync(new ConfirmReservationCommand(reservationId));
        await SendAsync(new SetReservationRequirementsCommand
        {
            Id = reservationId,
            Items = new List<ReservationRequirementItem>
            {
                new() { Kind = ReservationRequirementKind.Document, Label = "Driving licence" },
            }
        });

        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        var ask = (await SendAsync(new GetMyReservationRequirementsQuery(reservationId))).Single();

        return (customerId, agencyId, reservationId, ask.Id);
    }

    private static Task<string> StoreAsync(string relativePath) =>
        UsingScopeAsync(async services =>
        {
            var file = await services.GetRequiredService<IStoredFileService>().CreateAsync(
                new MemoryStream([9, 9, 9]), Path.GetFileName(relativePath), "image/png",
                DocumentType.PaymentProof, relativePath);

            await services.GetRequiredService<IApplicationDbContext>().SaveChangesAsync(CancellationToken.None);

            return file.Url;
        });

    private static Task AttachToAgencyAsync(string userId, int agencyId, bool mustChangePassword = false) =>
        UsingScopeAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            user!.AgencyId = agencyId;
            user.MustChangePassword = mustChangePassword;
            await userManager.UpdateAsync(user);
            return true;
        });

    private static async Task<HttpClient> SignedInClientAsync(string userId)
    {
        var tokens = await UsingScopeAsync(services =>
            services.GetRequiredService<ITokenService>().IssueTokensAsync(userId, CancellationToken.None));

        var client = CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return client;
    }
}
