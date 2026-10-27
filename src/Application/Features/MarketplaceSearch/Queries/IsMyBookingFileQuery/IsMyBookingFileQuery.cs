using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.IsMyBookingFileQuery
{
    // Whether a stored file is one the signed-in customer may open: a file they
    // sent in against one of their own bookings' requirements. Asked by the
    // uploads gate, which has no tenant to filter by for a customer — hence the
    // cross-tenant read, in the sanctioned folder. Matched on the URL rather than
    // the path's shape because an identical upload is deduplicated onto whatever
    // path the agency first stored those bytes under.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record IsMyBookingFileQuery(string Url) : IRequest<bool>;

    public class IsMyBookingFileQueryHandler : IRequestHandler<IsMyBookingFileQuery, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public IsMyBookingFileQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<bool> Handle(IsMyBookingFileQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            // Driven from the customer's own client rows (MarketplaceUserId is
            // indexed) down to their few requirements, so the URL comparison runs
            // over a handful of files, not the whole table — Url itself is too
            // wide for an index key.
            return await _context.Clients
                .IgnoreQueryFilters()
                .Where(c => c.MarketplaceUserId == userId)
                .SelectMany(c => c.Reservations!)
                .SelectMany(r => r.Requirements!)
                .AnyAsync(q => q.SubmittedFile != null && q.SubmittedFile.Url == request.Url, cancellationToken);
        }
    }
}
