namespace RemSolution.Application.Features.Agency.DTOs
{
    /// <summary>
    /// Whether an agency is live on the marketplace, and what it has to show if
    /// it goes. The counts travel with the verdict so "cannot publish yet" is
    /// answerable rather than merely refused.
    /// </summary>
    public class AgencyPublicationDto
    {
        public int AgencyId { get; init; }

        /// <summary>Instant (recorded from the clock) — the SPA renders it local.</summary>
        public DateTime? PublishedAt { get; init; }

        public bool IsPublished { get; init; }

        /// <summary>Cars that would actually appear: active, priced, not archived.</summary>
        public int OfferedCars { get; init; }

        /// <summary>Cars on the books, offered or not — the gap is the useful part.</summary>
        public int TotalCars { get; init; }

        public int Branches { get; init; }

        /// <summary>
        /// Whether the agency can go live now. False only for the one thing a
        /// shopfront cannot do without: something to offer.
        /// </summary>
        public bool CanPublish { get; init; }
    }
}
