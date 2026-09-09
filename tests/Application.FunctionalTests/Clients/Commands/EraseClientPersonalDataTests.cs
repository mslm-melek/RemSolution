using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Application.Features.Client.Commands.EraseClientPersonalDataCommand;
using RemSolution.Application.Features.Client.Commands.FlagClientCommand;
using RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand;
using RemSolution.Application.Features.Client.Queries.GetClientByIdQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class EraseClientPersonalDataTests : BaseTestFixture
{
    private static readonly byte[] CinBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] LicenceBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0B, 0x07 };

    private static string StoredPath(string url) =>
        Path.Combine(UploadsRoot, url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar));

    private static async Task<int> AddClientAsync() =>
        await SendAsync(new CreateClientCommand
        {
            FirstName = "Nadia",
            LastName = "Ben Salah",
            BirthDate = new DateTime(1988, 3, 4),
            BirthPlace = "Sousse",
            CIN = "09887766",
            PasseportNumber = "T1234567",
            DrivingLicenceNumber = "L-55-2019",
            Email = "nadia@example.tn",
            Description = "Regular customer",
        });

    private static Task<string> UploadAsync(int clientId, ClientDocumentType type, byte[] bytes) =>
        SendAsync(new UploadClientDocumentCommand
        {
            ClientId = clientId,
            DocumentType = type,
            FileName = "scan.png",
            ContentType = "image/png",
            Length = bytes.Length,
            Content = new MemoryStream(bytes)
        });

    private static Task<List<AuditLog>> AuditRowsAsync(int clientId) =>
        UsingScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<RemSolution.Infrastructure.Data.ApplicationDbContext>();
            var key = clientId.ToString();

            return await context.Set<AuditLog>()
                .Where(a => a.Entity == "Client" && a.EntityId == key)
                .ToListAsync();
        });

    [Test]
    public async Task ClearsEveryIdentityFieldAndNamesTheClientByItsId()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        await SendAsync(new EraseClientPersonalDataCommand(clientId, "Subject request"));

        var client = await FindAsync<Client>(clientId);

        client!.PersonalDataErasedAt.Should().NotBeNull();

        // Not blank: every list composes a label from these two, and a row
        // labelled with nothing reads as a defect.
        client.FirstName.Should().BeNull();
        client.LastName.Should().Be($"#{clientId}");

        client.BirthDate.Should().BeNull();
        client.BirthPlace.Should().BeNull();
        client.CIN.Should().BeNull();
        client.PasseportNumber.Should().BeNull();
        client.DrivingLicenceNumber.Should().BeNull();
        client.Email.Should().BeNull();
        client.Description.Should().BeNull();
        client.MarketplaceUserId.Should().BeNull();
        client.Notes.Should().BeNull();
        client.IsFlagged.Should().BeFalse();
    }

    [Test]
    public async Task DeletesTheDocumentScansAndTheirBytes()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        var cinUrl = await UploadAsync(clientId, ClientDocumentType.CIN, CinBytes);
        var licenceUrl = await UploadAsync(clientId, ClientDocumentType.DrivingLicence, LicenceBytes);

        File.Exists(StoredPath(cinUrl)).Should().BeTrue();

        await SendAsync(new EraseClientPersonalDataCommand(clientId));

        var client = await FindAsync<Client>(clientId);
        client!.CINFileId.Should().BeNull();
        client.DrivingLicenceFileId.Should().BeNull();
        client.PasseportFileId.Should().BeNull();
        client.CINPortraitFileId.Should().BeNull();

        // The scans are the most sensitive thing here, so the bytes go too, not
        // just the rows pointing at them.
        File.Exists(StoredPath(cinUrl)).Should().BeFalse();
        File.Exists(StoredPath(licenceUrl)).Should().BeFalse();

        (await CountAsync<StoredFile>()).Should().Be(0);
    }

    [Test]
    public async Task BlanksTheAuditPayloadsAboutTheClientButKeepsTheTrail()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        // FlagClient is [Auditable], and the interceptor serialises the WHOLE
        // client row either side of the change — so this one small edit puts a
        // full copy of the passport number into the audit trail.
        await SendAsync(new FlagClientCommand { Id = clientId, IsFlagged = true, Notes = "Late twice" });

        var before = await AuditRowsAsync(clientId);
        before.Should().NotBeEmpty();
        before.Should().Contain(a => a.Before != null && a.Before.Contains("T1234567"));

        await SendAsync(new EraseClientPersonalDataCommand(clientId, "Subject request"));

        var after = await AuditRowsAsync(clientId);

        // The trail survives — who edited this client, and when — but nothing it
        // said about the person does.
        after.Count.Should().BeGreaterThan(before.Count, "the erasure records itself");
        after.Should().Contain(a => a.Action == "FlagClient");

        after.Where(a => a.Action != "ErasePersonalData")
             .Should().OnlyContain(a => a.Before == null && a.After == null);

        var erasure = after.Single(a => a.Action == "ErasePersonalData");
        erasure.Before.Should().BeNull();
        erasure.After.Should().Contain("Subject request", "the operator's note is the only payload");
    }

    [Test]
    public async Task ClearsTheNotificationArgumentsAndAddressButKeepsTheRow()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        await AddAsync(new Notification
        {
            AgencyId = agencyId,
            Kind = NotificationKind.ReservationUpcoming,
            MessageKey = "reservationUpcoming",
            ClientId = clientId,
            RecipientEmail = "nadia@example.tn",
            ArgsJson = "{\"client\":\"Nadia Ben Salah\"}",
            DedupKey = $"erase-test-{clientId}",
            CreatedAt = DateTime.UtcNow
        });

        await SendAsync(new EraseClientPersonalDataCommand(clientId));

        var notifications = await AllAsync<Notification>();
        var row = notifications.Single(n => n.ClientId == clientId);

        row.ArgsJson.Should().BeNull();
        row.RecipientEmail.Should().BeNull();
        // The row itself is the agency's record that a message went out.
        row.MessageKey.Should().Be("reservationUpcoming");
    }

    [Test]
    public async Task ClearsTheNameLeftOnReviewsAndReportsButKeepsWhatTheySay()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        var car = new Car { Matricule = "ER-2", Status = CarStatus.Active };
        await AddAsync(car);

        var hire = RentingFixture.Hire(
            car.Id, clientId,
            new DateTime(2020, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 5, 4, 0, 0, 0, DateTimeKind.Utc),
            RentingState.Done, price: Money.Of(300m, "TND"));
        await AddAsync(hire);

        // Both snapshot the customer's name at submit time so the row stays
        // readable after a rename — which is also what would carry the name past
        // an erasure. Neither is an ITenantEntity, so no tenant read finds them.
        await AddAsync(new AgencyReview
        {
            AgencyId = agencyId,
            RentingId = hire.Id,
            ClientId = clientId,
            AuthorUserId = "portal-user-1",
            AuthorName = "Nadia Ben Salah",
            CarName = "Renault Clio",
            Rating = 2,
            Comment = "The car turned up late.",
            SubmittedAt = DateTime.UtcNow
        });

        await AddAsync(AgencyReport.Create(
            agencyId,
            AgencyReportKind.ServiceQuality,
            "Nobody was at the counter.",
            DateTime.UtcNow,
            rentingId: hire.Id,
            clientId: clientId,
            reporterUserId: "portal-user-1",
            reporterName: "Nadia Ben Salah",
            bookingSummary: "Renault Clio, 1-4 May"));

        await SendAsync(new EraseClientPersonalDataCommand(clientId));

        var review = (await AllAsync<AgencyReview>()).Single(v => v.ClientId == clientId);

        review.AuthorName.Should().BeNull("the marketplace shows it beside the rating");
        review.AuthorUserId.Should().BeNull();

        // A rating and a complaint are records of the AGENCY's conduct, not of
        // the person, so what they say survives — the same reasoning that keeps
        // the notification rows.
        review.Rating.Should().Be(2);
        review.Comment.Should().Be("The car turned up late.");

        var report = (await AllAsync<AgencyReport>()).Single(r => r.ClientId == clientId);

        report.ReporterName.Should().BeNull();
        report.ReporterUserId.Should().BeNull();
        report.Message.Should().Be("Nobody was at the counter.");

        // Kept on purpose: it names a car and two dates, never a person, and it
        // is all the arbitrator can see of a booking they cannot read.
        report.BookingSummary.Should().Be("Renault Clio, 1-4 May");
    }

    [Test]
    public async Task KeepsTheFinancialRecords()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        var car = new Car { Matricule = "ER-1", Status = CarStatus.Active };
        await AddAsync(car);

        var hire = RentingFixture.Hire(
            car.Id, clientId,
            new DateTime(2020, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            RentingState.Done, price: Money.Of(400m, "TND"));
        await AddAsync(hire);

        await SendAsync(new EraseClientPersonalDataCommand(clientId));

        // A rental agency has to keep its books; the hire still names the row it
        // was let to, which is exactly why the client row is emptied, not deleted.
        var kept = await FindAsync<Renting>(hire.Id);
        kept.Should().NotBeNull();
        kept!.ClientId.Should().Be(clientId);
        kept.Price!.Amount.Should().Be(400m);
    }

    [Test]
    public async Task RefusesWhileTheClientHasABookingInProgress()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        var car = new Car { Matricule = "ER-LIVE", Status = CarStatus.Active };
        await AddAsync(car);

        await AddAsync(RentingFixture.Hire(
            car.Id, clientId,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(3),
            RentingState.InProgress, price: Money.Of(300m, "TND")));

        await FluentActions.Invoking(() =>
                SendAsync(new EraseClientPersonalDataCommand(clientId)))
            .Should().ThrowAsync<FluentValidation.ValidationException>();

        var client = await FindAsync<Client>(clientId);
        client!.PersonalDataErasedAt.Should().BeNull();
        client.CIN.Should().Be("09887766", "nothing was erased");
    }

    [Test]
    public async Task ReachesAClientWhoWasAlreadyArchived()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        // Archived a year ago and invisible to every ordinary read — and the one
        // whose scans nobody will ever look at again.
        await SendAsync(new DeleteClientCommand(clientId));
        (await FindAsync<Client>(clientId)).Should().BeNull();

        await SendAsync(new EraseClientPersonalDataCommand(clientId, "Subject request"));

        var archived = await FindIgnoringFiltersAsync<Client>(c => c.Id == clientId);
        archived!.IsDeleted.Should().BeTrue();
        archived.PersonalDataErasedAt.Should().NotBeNull();
        archived.CIN.Should().BeNull();
    }

    [Test]
    public async Task IsIdempotent()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();

        await SendAsync(new EraseClientPersonalDataCommand(clientId, "First"));
        var erasedAt = (await FindAsync<Client>(clientId))!.PersonalDataErasedAt;

        await SendAsync(new EraseClientPersonalDataCommand(clientId, "Second"));

        (await FindAsync<Client>(clientId))!.PersonalDataErasedAt.Should().Be(erasedAt);

        var rows = await AuditRowsAsync(clientId);
        rows.Count(a => a.Action == "ErasePersonalData")
            .Should().Be(1, "a retried request does not record a second erasure");
    }

    [Test]
    public async Task IsNotSomethingClientDeleteAloneAllows()
    {
        // Client.Delete only archives, and can be undone; erasing is its own
        // permission precisely so the two are not the same grant.
        await RunAsAgencyStaffAsync(Permissions.ClientRead, Permissions.ClientDelete);
        await AddTestAgencyAsync();

        var client = new Client { FirstName = "Nadia", LastName = "Ben Salah", CIN = "09887766" };
        await AddAsync(client);

        await FluentActions.Invoking(() =>
                SendAsync(new EraseClientPersonalDataCommand(client.Id)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task TheClientScreenSaysTheDataWasErased()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await AddClientAsync();
        await SendAsync(new EraseClientPersonalDataCommand(clientId));

        var dto = await SendAsync(new GetClientByIdQuery(clientId));

        dto.PersonalDataErasedAt.Should().NotBeNull();
        dto.CIN.Should().BeNull();
    }
}
