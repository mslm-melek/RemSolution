using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencyReport.DTOs
{
    // One line on a report's thread, as all three readers see it. No author id
    // or name: there are only two parties, FromPlatform says which, and Mine
    // says whether the reader wrote it — two platform administrators are both
    // "the platform", but only one of them is "you".
    public class AgencyReportMessageDto
    {
        public int Id { get; init; }
        public AgencyReportMessageKind Kind { get; init; }
        public bool FromPlatform { get; init; }
        public bool Mine { get; init; }

        /// <summary>Empty once the customer's personal data was erased.</summary>
        public string? Body { get; init; }

        /// <summary>Instant, not a wall-clock date — the SPA renders it local.</summary>
        public DateTime SentAt { get; init; }
    }

    /// <summary>
    /// Reads the threads of a page of reports, for all three report queries, so
    /// the customer, the agency and the platform cannot be shown different ones.
    /// <para>
    /// A second round trip rather than a collection inside the list projection:
    /// that would be one join repeating every report column once per message,
    /// on every page load, and split queries are a relational-provider API the
    /// Application layer does not reference.
    /// </para>
    /// </summary>
    public static class AgencyReportThreads
    {
        public static async Task<ILookup<int, AgencyReportMessageDto>> LoadAsync(
            IApplicationDbContext context,
            IReadOnlyCollection<int> reportIds,
            string? readerUserId,
            CancellationToken cancellationToken)
        {
            if (reportIds.Count == 0)
            {
                return Array.Empty<AgencyReportMessageDto>().ToLookup(_ => 0);
            }

            var rows = await context.AgencyReports
                .AsNoTracking()
                .Where(r => reportIds.Contains(r.Id))
                .SelectMany(r => r.Messages)
                .OrderBy(m => m.SentAt)
                .ThenBy(m => m.Id)
                .Select(m => new
                {
                    m.AgencyReportId,
                    Message = new AgencyReportMessageDto
                    {
                        Id = m.Id,
                        Kind = m.Kind,
                        FromPlatform = m.FromPlatform,
                        Mine = readerUserId != null && m.AuthorUserId == readerUserId,
                        Body = m.Body,
                        SentAt = m.SentAt,
                    },
                })
                .ToListAsync(cancellationToken);

            return rows.ToLookup(r => r.AgencyReportId, r => r.Message);
        }

        /// <summary>
        /// Who wrote the last actual message — true the platform, false the
        /// customer, null nobody yet. Withdrawals and reopenings are events, not
        /// replies, so they neither answer the platform nor ask it anything.
        /// </summary>
        public static bool? LastWriterIsPlatform(IEnumerable<AgencyReportMessageDto> thread) =>
            thread.LastOrDefault(m => m.Kind == AgencyReportMessageKind.Message)?.FromPlatform;
    }
}
