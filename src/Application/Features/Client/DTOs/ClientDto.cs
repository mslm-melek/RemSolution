namespace RemSolution.Application.Features.Client.DTOs
{
    public class ClientDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
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

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Client, ClientDto>()
                      .Map(dest => dest.BirthCountryName, src => src.BirthCountry != null ? src.BirthCountry.Name : null)
                      // Document URLs now live on StoredFile records; surface the
                      // plain URL so the API contract is unchanged for readers.
                      .Map(dest => dest.CINImageUrl, src => src.CINFile != null ? src.CINFile.Url : null)
                      .Map(dest => dest.DrivingLicenceImageUrl, src => src.DrivingLicenceFile != null ? src.DrivingLicenceFile.Url : null)
                      .Map(dest => dest.PasserportImageUrl, src => src.PasseportFile != null ? src.PasseportFile.Url : null);
            }
        }
    }
}
