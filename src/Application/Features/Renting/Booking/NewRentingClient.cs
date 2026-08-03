using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Client.Validation;

namespace RemSolution.Application.Features.Renting.Booking
{
    /// <summary>
    /// A client created inline while booking — the walk-in case, where forcing the
    /// agent to leave the booking screen, create the client and come back is the
    /// wrong shape. Used for the renter and for the second driver, on both
    /// <c>CreateRentingCommand</c> and <c>UpdateRentingCommand</c>: "a person we do
    /// not have on file yet" is the same shape in all four places, which is why this
    /// type lives here rather than next to one of them.
    /// <para>
    /// Implements <see cref="IClientPayload"/> so the SAME identity-document rules
    /// the standalone client commands use apply here, with no second copy to drift.
    /// The full field set is accepted even though the booking screen only asks for
    /// the essentials: the API should not be narrower than the entity, and the
    /// remaining fields are edited later on the client page.
    /// </para>
    /// <para>
    /// The document image FKs are deliberately absent, for the same reason
    /// <c>CreateClientCommand</c> omits them: uploads own the stored-file
    /// lifecycle.
    /// </para>
    /// </summary>
    public record NewRentingClient : IClientPayload
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        // Supplying this is what gets the customer a login to follow the booking
        // they are standing at the counter making — for the RENTER. Set on a second
        // driver it is stored on their record but provisions nothing, because the
        // portal lists a customer's bookings by renter (see GetMyRentingsQuery).
        public string? Email { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? BirthPlace { get; init; }
        public int? BirthCountryId { get; init; }
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
        public string? Description { get; init; }
    }
}

namespace RemSolution.Application.Features.Renting.Booking
{
    /// <summary>
    /// Applies the shared client rules to the inline payload; see
    /// <see cref="Application.Features.Client.Validation.ClientPayloadValidator{T}"/>.
    /// </summary>
    public class NewRentingClientValidator : ClientPayloadValidator<NewRentingClient>
    {
        public NewRentingClientValidator(
            IApplicationDbContext context, TimeProvider dateTime, ILocalizer localizer)
            : base(context, dateTime, localizer)
        {
            RuleFor(c => c.Description).MaximumLength(1000);
        }
    }
}
