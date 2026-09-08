using System.Linq.Expressions;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Common.Agencies;

/// <summary>
/// Counts an agency's reliability off the rows that already record it — nothing
/// is stored, the same reasoning as the client score: a counter would be a
/// second version of the truth to keep in step.
/// <para>
/// The caller supplies the queryables because the two readers see the tables
/// differently: the public shopfront reads them cross-tenant (an anonymous
/// visitor has no tenant claim), the agency's own screen reads them filtered.
/// The predicates below are shared so the two cannot disagree about what a
/// broken booking is.
/// </para>
/// </summary>
public static class AgencyReliabilityCounts
{
    /// <summary>
    /// Every hold the agency ever said yes to. A cancelled one counts here only
    /// if it had been confirmed first — a request nobody answered was never a
    /// promise (see <c>Reservation.CancelledAfterConfirmation</c>).
    /// <para>
    /// The same test as <c>AgencyReport.CanReport</c>, written as an expression
    /// because EF has to translate this one into SQL: a booking worth scoring is
    /// a booking worth complaining about.
    /// </para>
    /// </summary>
    public static readonly Expression<Func<Reservation, bool>> WasConfirmed = r =>
        r.Status == ReservationStatus.Confirmed
        || r.Status == ReservationStatus.Paid
        || r.Status == ReservationStatus.Converted
        || (r.Status == ReservationStatus.Cancelled && r.CancelledAfterConfirmation);

    /// <summary>
    /// A promise the agency broke: it had confirmed the hold and then called it
    /// off itself. A customer cancelling their own confirmed hold is not this.
    /// </summary>
    public static readonly Expression<Func<Reservation, bool>> BrokenByAgency = r =>
        r.Status == ReservationStatus.Cancelled
        && r.CancelledAfterConfirmation
        && !r.CancelledByCustomer;

    public static async Task<AgencyReliability> ComputeAsync(
        IQueryable<Reservation> reservations,
        IQueryable<AgencyReport> reports,
        int agencyId,
        CancellationToken cancellationToken)
    {
        // One pass: the confirmed bookings split into the ones the agency broke
        // and the rest, so the score and its denominator come from the same read.
        var tally = await reservations
            .Where(r => r.AgencyId == agencyId)
            .Where(WasConfirmed)
            .GroupBy(BrokenByAgency)
            .Select(g => new { Broken = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var upheld = await reports
            .Where(r => r.AgencyId == agencyId && r.Status == AgencyReportStatus.Upheld)
            .CountAsync(cancellationToken);

        return new AgencyReliability(
            ConfirmedBookings: tally.Sum(t => t.Count),
            CancelledByAgency: tally.Where(t => t.Broken).Sum(t => t.Count),
            UpheldReports: upheld);
    }
}
