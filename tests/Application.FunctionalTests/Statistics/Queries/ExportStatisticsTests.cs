using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Reporting;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Statistics.DTOs;
using RemSolution.Application.Features.Statistics.Queries.ExportStatisticsQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Statistics.Queries;

using static Testing;

public class ExportStatisticsTests : BaseTestFixture
{
    private static readonly DateTime YearStart = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime YearEnd = new(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InFebruary = new(2030, 2, 10, 9, 0, 0, DateTimeKind.Utc);

    // The .xlsx and .pdf magic numbers: a zip container and "%PDF".
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF");

    private async Task<Car> AddCarAsync(string matricule)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);
        return car;
    }

    private async Task<Renting> AddHireAsync(
        Car car, Client client, DateTime start, int days, decimal price)
    {
        var renting = RentingFixture.Hire(
            car.Id, client.Id, start, start.AddDays(days), RentingState.Done,
            price: Money.Of(price, "TND"));
        await AddAsync(renting);
        return renting;
    }

    private async Task<(Car Car, Client Client)> AddActivityAsync()
    {
        var car = await AddCarAsync("EX-1");
        var client = new Client { FirstName = "Export", LastName = "Stats" };
        await AddAsync(client);

        await AddHireAsync(car, client, InFebruary, days: 3, price: 300m);

        var type = new ExpenseTypeEntity { Name = "Tyres", IsActive = true };
        await AddAsync(type);
        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, ExpenseDate = InFebruary, Amount = 50m
        });

        return (car, client);
    }

    private static async Task<byte[]> BytesAsync(FileDownload download)
    {
        using var buffer = new MemoryStream();
        await download.Content.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    [Test]
    public async Task ProducesAWorkbookNamedAfterTheWindow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddActivityAsync();

        var download = await SendAsync(new ExportStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month,
            Format: StatisticsExportFormat.Xlsx));

        download.FileName.Should().Be("statistics-2030-01-2030-12.xlsx");
        download.ContentType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await BytesAsync(download);
        bytes.Should().StartWith(ZipHeader);
    }

    [Test]
    public async Task ProducesAPdf()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddActivityAsync();

        var download = await SendAsync(new ExportStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month,
            Format: StatisticsExportFormat.Pdf));

        download.FileName.Should().Be("statistics-2030-01-2030-12.pdf");
        download.ContentType.Should().Be("application/pdf");

        var bytes = await BytesAsync(download);
        bytes.Should().StartWith(PdfHeader);
    }

    [Test]
    public async Task TheFileNameNamesTheCarWhenTheReportIsFilteredToOne()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (car, _) = await AddActivityAsync();

        var download = await SendAsync(new ExportStatisticsQuery(
            CarId: car.Id, From: YearStart, To: YearEnd,
            Granularity: StatisticsGranularity.Month,
            Format: StatisticsExportFormat.Xlsx));

        download.FileName.Should().Be("statistics-ex1-2030-01-2030-12.xlsx");
    }

    [Test]
    public async Task AYearlyReportIsNamedByYearsNotMonths()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddActivityAsync();

        var download = await SendAsync(new ExportStatisticsQuery(
            From: YearStart, To: new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity: StatisticsGranularity.Year,
            Format: StatisticsExportFormat.Xlsx));

        download.FileName.Should().Be("statistics-2030-2031.xlsx");
    }

    [Test]
    public async Task TheWorkbookHoldsTheSameFiguresTheScreenShows()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddActivityAsync();

        var download = await SendAsync(new ExportStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month,
            Format: StatisticsExportFormat.Xlsx));

        using var workbook = new XLWorkbook(new MemoryStream(await BytesAsync(download)));

        // By period, then by car — the two tables of the fleet report.
        workbook.Worksheets.Count.Should().Be(2);

        var periods = workbook.Worksheet(1);

        // Four rows of chrome, twelve months, then the totals line.
        const int headerRow = 4;
        const int february = headerRow + 2;
        const int totals = headerRow + 12 + 1;

        periods.Cell(headerRow, 1).GetString().Should().NotBeEmpty();

        periods.Cell(february, 2).GetValue<int>().Should().Be(1, "one hire started in February");
        periods.Cell(february, 3).GetValue<int>().Should().Be(3);
        periods.Cell(february, 4).GetValue<decimal>().Should().Be(300m);
        periods.Cell(february, 6).GetValue<decimal>().Should().Be(50m);
        periods.Cell(february, 7).GetValue<decimal>().Should().Be(250m);

        // The figures are numbers, not text — the whole point of this format.
        periods.Cell(february, 4).DataType.Should().Be(XLDataType.Number);

        periods.Cell(totals, 4).GetValue<decimal>().Should().Be(300m);
        periods.Cell(totals, 7).GetValue<decimal>().Should().Be(250m);

        var byCar = workbook.Worksheet(2);
        byCar.Cell(headerRow + 1, 1).GetString().Should().Contain("EX-1");
        byCar.Cell(headerRow + 1, 4).GetValue<decimal>().Should().Be(300m);
    }

    [Test]
    public async Task AnEmptyWindowStillProducesAFile()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // No cars, no hires, no expenses: the file is the answer "nothing
        // happened", which is a report an agency does ask for.
        var download = await SendAsync(new ExportStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month,
            Format: StatisticsExportFormat.Xlsx));

        var bytes = await BytesAsync(download);
        bytes.Should().StartWith(ZipHeader);
        bytes.Length.Should().BeGreaterThan(0);
    }

    // Arabic is the case both renderers treat specially (right-to-left sheets, a
    // host font for the PDF), and the one a build machine has no font for — which
    // is exactly why glyph checking is off. Neither may fail the request.
    [TestCase(StatisticsExportFormat.Xlsx)]
    [TestCase(StatisticsExportFormat.Pdf)]
    public async Task RendersInArabicWithoutFailingOnFontsOrDirection(
        StatisticsExportFormat format)
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddActivityAsync();

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(Languages.Arabic);

        try
        {
            var download = await SendAsync(new ExportStatisticsQuery(
                From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month,
                Format: format));

            var bytes = await BytesAsync(download);
            bytes.Should().StartWith(format == StatisticsExportFormat.Pdf ? PdfHeader : ZipHeader);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Test]
    public async Task AnUnknownFormatIsRefusedByValidationRatherThanReachingTheRenderers()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var query = new ExportStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month,
            Format: (StatisticsExportFormat)99);

        await FluentActions.Invoking(() => SendAsync(query))
            .Should().ThrowAsync<RemSolution.Application.Common.Exceptions.ValidationException>();
    }
}
