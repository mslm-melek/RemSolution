using FluentAssertions;
using NUnit.Framework;
using RemSolution.Infrastructure.Data.Converters;

namespace RemSolution.Application.UnitTests.Common.Data;

public class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _sut = new();

    [Test]
    public void ToProvider_ConvertsLocalToUtc()
    {
        var local = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

        var stored = (DateTime)_sut.ConvertToProvider(local)!;

        stored.Should().Be(local.ToUniversalTime());
    }

    [Test]
    public void ToProvider_AssumesUnspecifiedIsAlreadyUtc()
    {
        var unspecified = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var stored = (DateTime)_sut.ConvertToProvider(unspecified)!;

        // Same clock value, just tagged UTC — not shifted by the server offset.
        stored.Should().Be(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToProvider_LeavesUtcUnchanged()
    {
        var utc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var stored = (DateTime)_sut.ConvertToProvider(utc)!;

        stored.Should().Be(utc);
    }

    [Test]
    public void FromProvider_StampsUtcKind()
    {
        // datetime2 round-trips without a Kind.
        var fromDb = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var materialised = (DateTime)_sut.ConvertFromProvider(fromDb)!;

        materialised.Kind.Should().Be(DateTimeKind.Utc);
    }
}
