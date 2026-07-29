using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyRentingsQuery
{
    // The signed-in customer's rentals across ALL agencies, newest first — the
    // "my trips" list, and the only place a rating can be started from. Scoped by
    // the Client → MarketplaceUserId link, so a customer only ever sees their own.
    // Cross-tenant read, which is why it lives here (see TenantEnforcementTests).
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyRentingsQuery : IRequest<IList<MyRentingDto>>;

    public class GetMyRentingsQueryHandler : IRequestHandler<GetMyRentingsQuery, IList<MyRentingDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public GetMyRentingsQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IList<MyRentingDto>> Handle(
            GetMyRentingsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            return await _context.Rentings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Client != null && r.Client.MarketplaceUserId == userId)
                .OrderByDescending(r => r.StartDate)
                .Select(r => new MyRentingDto
                {
                    RentingId = r.Id,
                    AgencyId = r.AgencyId,
                    AgencyName = r.Agency != null ? r.Agency.Name : null,
                    CarBrandName = r.Car != null && r.Car.Model != null && r.Car.Model.Brand != null
                        ? r.Car.Model.Brand.Name
                        : null,
                    CarModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                    CarImageUrl = r.Car == null
                        ? null
                        : r.Car.Images!.Where(i => i.IsPrimary && i.MediumFile != null)
                                       .Select(i => i.MediumFile!.Url)
                                       .FirstOrDefault(),
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    RentingState = r.RentingState,
                    Price = r.Price == null ? null : new MoneyDto(r.Price.Amount, r.Price.Currency),

                    // Repeats AgencyReview.CanReview inline: EF has to translate
                    // the test into SQL, so the rule cannot be called here.
                    // Change it there and this changes with it.
                    CanReview = r.RentingState == RentingState.Done
                                && !_context.AgencyReviews.Any(v => v.RentingId == r.Id),

                    MyRating = _context.AgencyReviews
                        .Where(v => v.RentingId == r.Id)
                        .Select(v => (int?)v.Rating)
                        .FirstOrDefault(),
                    MyComment = _context.AgencyReviews
                        .Where(v => v.RentingId == r.Id)
                        .Select(v => v.Comment)
                        .FirstOrDefault(),
                    ReviewedAt = _context.AgencyReviews
                        .Where(v => v.RentingId == r.Id)
                        .Select(v => (DateTime?)v.SubmittedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
        }
    }
}
