using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Agency.Models
{
    /// <summary>
    /// One branch as the new-agency form submits it, so an agency can be created
    /// with its locations in a single transaction. Creation only — there is no
    /// id, and nothing to delete yet. Editing the branches of an existing agency
    /// goes through the <c>Agencies/{id}/branches</c> sub-resource instead.
    /// </summary>
    public record AgencyBranchInput
    {
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public string? Address { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    public class AgencyBranchInputValidator : AbstractValidator<AgencyBranchInput>
    {
        public AgencyBranchInputValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();

            RuleFor(v => v.CountryId)
                .GreaterThan(0);

            RuleFor(v => v.Address)
                .MaximumLength(500);

            RuleFor(v => v.Latitude)
                .InclusiveBetween(-90, 90)
                .When(v => v.Latitude.HasValue);

            RuleFor(v => v.Longitude)
                .InclusiveBetween(-180, 180)
                .When(v => v.Longitude.HasValue);

            RuleFor(v => v)
                .Must(v => v.Latitude.HasValue == v.Longitude.HasValue)
                .WithMessage(_ => localizer["Validation.Coordinates.Together"]);
        }
    }
}
