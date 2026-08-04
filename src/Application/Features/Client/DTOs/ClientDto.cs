using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Client.DTOs
{
    public class ClientDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        // Optimistic-concurrency token; echoed back on update (see P.8).
        public byte[]? RowVersion { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? BirthPlace { get; init; }
        public int? BirthCountryId { get; init; }
        public string? BirthCountryName { get; init; }
        public string? CIN { get; init; }
        public DateTime? CINDeliveranceDate { get; init; }
        public string? CINDeliverancePlace { get; init; }
        public int? CINDeliveranceCountryId { get; init; }
        public string? PasseportNumber { get; init; }
        public DateTime? PasseportDeliveranceDate { get; init; }
        public string? PasseportDeliverancePlace { get; init; }
        public int? PasseportDeliveranceCountryId { get; init; }
        public string? DrivingLicenceNumber { get; init; }
        public DateTime? DrivingLicenceDeliveranceDate { get; init; }
        public string? DrivingLicenceDeliverancePlace { get; init; }
        public int? DrivingLicenceDeliveranceCountryId { get; init; }
        public string? CINImageUrl { get; init; }
        public string? DrivingLicenceImageUrl { get; init; }
        public string? PasserportImageUrl { get; init; }
        public string? Description { get; init; }
        // Per-agency bad-client flag and its moderation notes (see Client entity).
        public bool IsFlagged { get; init; }
        public string? Notes { get; init; }
        public string? MarketplaceUserId { get; init; }

        /// <summary>
        /// Whether this client can sign in to the customer portal. The raw
        /// MarketplaceUserId above is an Identity key the UI has no use for;
        /// this is the question the client screen actually asks, and what the
        /// Invite action is shown or hidden on.
        /// </summary>
        public bool HasPortalAccount { get; init; }

        /// <summary>
        /// Hires this client has been on, cancelled ones excluded — the size of
        /// their history. Both seats count, renter and second driver, because this
        /// figure is the label on a link into the rentings list, and that list's
        /// client filter matches either seat (see GetRentingsWithPaginationQuery):
        /// a count that disagreed with the rows it opens would just look wrong.
        /// </summary>
        public int RentingCount { get; init; }

        /// <summary>Hires not yet finished (upcoming or ongoing) — what is still running.</summary>
        public int OpenRentingCount { get; init; }

        /// <summary>
        /// Hires still out past their agreed end date. This is what the "remind
        /// this client they are late" action is offered on, so the list only shows
        /// it where there is something to write about.
        /// <para>
        /// Only the renter's own seat counts, not hires they were second driver
        /// on: the letter goes to whoever signed the contract.
        /// </para>
        /// </summary>
        public int OverdueRentingCount { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Client, ClientDto>()
                      .Map(dest => dest.BirthCountryName, src => src.BirthCountry != null ? src.BirthCountry.Name : null)
                      // Projected in SQL, like the credits screen's per-client sums.
                      // Money is deliberately NOT here: what a client owes is the
                      // Credits module's answer and stays behind Credit.Read (see
                      // GetClientCreditsQuery). The null checks are for the
                      // in-memory adapter, which a plain entity with no collection
                      // loaded goes through (see MappingTests).
                      .Map(dest => dest.RentingCount,
                           src => (src.Rentings == null
                                      ? 0
                                      : src.Rentings.Count(r => r.RentingState != RentingState.Cancelled))
                                  + (src.SecondRentings == null
                                      ? 0
                                      : src.SecondRentings.Count(r => r.RentingState != RentingState.Cancelled)))
                      .Map(dest => dest.OpenRentingCount,
                           src => (src.Rentings == null
                                      ? 0
                                      : src.Rentings.Count(r => r.RentingState == RentingState.NotYet
                                                                || r.RentingState == RentingState.InProgress))
                                  + (src.SecondRentings == null
                                      ? 0
                                      : src.SecondRentings.Count(r => r.RentingState == RentingState.NotYet
                                                                     || r.RentingState == RentingState.InProgress)))
                      // DateTime.UtcNow rather than an injected TimeProvider: a
                      // Mapster projection has nothing injected into it, and EF
                      // translates this to GETUTCDATE() so the comparison happens
                      // in SQL alongside the rest of the count.
                      .Map(dest => dest.OverdueRentingCount,
                           src => src.Rentings == null
                                      ? 0
                                      : src.Rentings.Count(r => r.RentingState == RentingState.InProgress
                                                                && r.EndDate != null
                                                                && r.EndDate < DateTime.UtcNow))
                      .Map(dest => dest.HasPortalAccount, src => src.MarketplaceUserId != null)
                      // Document URLs now live on StoredFile records; surface the
                      // plain URL so the API contract is unchanged for readers.
                      .Map(dest => dest.CINImageUrl, src => src.CINFile != null ? src.CINFile.Url : null)
                      .Map(dest => dest.DrivingLicenceImageUrl, src => src.DrivingLicenceFile != null ? src.DrivingLicenceFile.Url : null)
                      .Map(dest => dest.PasserportImageUrl, src => src.PasseportFile != null ? src.PasseportFile.Url : null);
            }
        }
    }
}
