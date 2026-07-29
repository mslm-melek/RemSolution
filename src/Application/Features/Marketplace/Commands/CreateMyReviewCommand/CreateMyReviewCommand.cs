using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.CreateMyReviewCommand
{
    // A customer rates the agency they rented from, once per finished rental.
    // The customer has no tenant, so the renting is loaded cross-tenant and
    // identity is proven by the link that already exists: the renting's Client
    // row must carry this user's MarketplaceUserId.
    //
    // Deliberately NOT gated on any agency feature: the review is the customer's
    // own account of a rental that already happened, and an agency must not be
    // able to switch off the reputation it earned by dropping a module.
    //
    // Unlike the booking and chat commands, this one does NOT push AmbientTenant:
    // AgencyReview is platform-level public content (see AgencyReview), so there
    // is no tenant filter to satisfy and no AgencyId stamp to inherit — the
    // agency is written explicitly, copied from the renting.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record CreateMyReviewCommand : IRequest<int>
    {
        public int RentingId { get; init; }
        public int Rating { get; init; }
        public string? Comment { get; init; }
    }

    public class CreateMyReviewCommandHandler : IRequestHandler<CreateMyReviewCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public CreateMyReviewCommandHandler(
            IApplicationDbContext context, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateMyReviewCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var renting = await _context.Rentings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == request.RentingId
                            && r.Client != null
                            && r.Client.MarketplaceUserId == userId)
                .Select(r => new
                {
                    r.Id,
                    r.AgencyId,
                    r.RentingState,
                    r.ClientId,
                    ClientFirstName = r.Client!.FirstName,
                    ClientLastName = r.Client!.LastName,
                    CarBrandName = r.Car != null && r.Car.Model != null && r.Car.Model.Brand != null
                        ? r.Car.Model.Brand.Name
                        : null,
                    CarModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Someone else's renting is indistinguishable from a missing one.
            Guard.Against.NotFound(request.RentingId, renting);

            if (!AgencyReview.CanReview(renting.RentingState))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.RentingId),
                        "You can only rate a rental once it is finished.")
                });
            }

            // The unique index on RentingId is the real guarantee; this check
            // turns the race that loses into a readable message instead of a 500.
            var alreadyReviewed = await _context.AgencyReviews
                .AnyAsync(r => r.RentingId == renting.Id, cancellationToken);

            if (alreadyReviewed)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.RentingId),
                        "You have already rated this rental.")
                });
            }

            var carName = string.Join(' ', new[] { renting.CarBrandName, renting.CarModelName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            var review = new AgencyReview
            {
                AgencyId = renting.AgencyId,
                RentingId = renting.Id,
                ClientId = renting.ClientId,
                AuthorUserId = userId,
                AuthorName = ((renting.ClientFirstName ?? string.Empty)
                    + " " + (renting.ClientLastName ?? string.Empty)).Trim(),
                CarName = carName.Length == 0 ? null : carName,
                Rating = request.Rating,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                SubmittedAt = _dateTime.GetUtcNow().UtcDateTime,
            };

            _context.AgencyReviews.Add(review);
            await _context.SaveChangesAsync(cancellationToken);

            return review.Id;
        }
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.CreateMyReviewCommand
{
    public class CreateMyReviewCommandValidator : AbstractValidator<CreateMyReviewCommand>
    {
        public CreateMyReviewCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.Rating).InclusiveBetween(AgencyReview.MinRating, AgencyReview.MaxRating);
            RuleFor(v => v.Comment).MaximumLength(AgencyReview.MaxCommentLength);
        }
    }
}
