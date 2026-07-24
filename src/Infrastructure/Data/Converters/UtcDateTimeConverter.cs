using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RemSolution.Infrastructure.Data.Converters;

/// <summary>
/// Enforces the project's UTC-at-the-persistence-boundary rule for domain
/// <see cref="DateTime"/> values (see docs/PROJECT_OVERVIEW.md, "Time").
/// SQL Server's datetime2 stores no offset, so without this every read comes
/// back as <see cref="DateTimeKind.Unspecified"/> and serializes without a
/// trailing 'Z'. Applied to every DateTime/DateTime? property in
/// ApplicationDbContext.ConfigureConventions; DateTimeOffset audit stamps are
/// already unambiguous and untouched.
///
/// Write: a Local value is converted to UTC; an Unspecified value is assumed to
/// already be UTC (the API edge deserializes inbound values as UTC), so its
/// Kind is stamped rather than shifted. Read: the value is stamped UTC.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
