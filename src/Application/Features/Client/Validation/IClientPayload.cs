namespace RemSolution.Application.Features.Client.Validation
{
    // Shared shape of the client write commands (create/update) so both
    // validators can apply the same identity-document rules.
    public interface IClientPayload
    {
        string FirstName { get; }
        string LastName { get; }
        DateTime? BirthDate { get; }
        string? BirthPlace { get; }
        int? BirthCountryId { get; }
        string? CIN { get; }
        DateTime? CINDeliveranceDate { get; }
        string? CINDeliverancePlace { get; }
        int? CINDeliveranceCountryId { get; }
        string? PasseportNumber { get; }
        DateTime? PasseportDeliveranceDate { get; }
        string? PasseportDeliverancePlace { get; }
        int? PasseportDeliveranceCountryId { get; }
        string? DrivingLicenceNumber { get; }
        DateTime? DrivingLicenceDeliveranceDate { get; }
        string? DrivingLicenceDeliverancePlace { get; }
        int? DrivingLicenceDeliveranceCountryId { get; }
    }
}
