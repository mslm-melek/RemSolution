using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery
{
    // The signed-in customer's reservations across ALL agencies. Cross-tenant
    // read (lives under Features/MarketplaceSearch/, the sanctioned location).
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyReservationsQuery : IRequest<IList<MyReservationDto>>;

    public class GetMyReservationsQueryHandler : IRequestHandler<GetMyReservationsQuery, IList<MyReservationDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public GetMyReservationsQueryHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings,
            IUser user, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<IList<MyReservationDto>> Handle(GetMyReservationsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var rows = await _context.Reservations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Client != null && r.Client.MarketplaceUserId == userId)
                .OrderByDescending(r => r.StartDate)
                .ProjectToType<MyReservationDto>()
                .ToListAsync(cancellationToken);

            await FillCancellationTermsAsync(rows, cancellationToken);

            return rows;
        }

        /// <summary>
        /// What cancelling would cost right now, answered by the same
        /// <see cref="CancellationPolicy"/> the cancel command applies — the
        /// customer must not be quoted one figure and charged another. Settings
        /// are read per agency (cached), and a customer deals with a handful.
        /// </summary>
        private async Task FillCancellationTermsAsync(
            IEnumerable<MyReservationDto> rows, CancellationToken cancellationToken)
        {
            var now = _dateTime.GetUtcNow().UtcDateTime;
            var byAgency = new Dictionary<int, AgencySettingsSnapshot>();

            foreach (var dto in rows)
            {
                if (!byAgency.TryGetValue(dto.AgencyId, out var settings))
                {
                    settings = await _settings.GetAsync(dto.AgencyId, cancellationToken);
                    byAgency[dto.AgencyId] = settings;
                }

                var isActive = dto.Status is ReservationStatus.PendingConfirmation
                    or ReservationStatus.Confirmed or ReservationStatus.Paid;

                // The hard cutoff: inside it, neither side can call the booking
                // off any more.
                var pastCutoff = dto.StartDate is DateTime start
                    && now >= start.AddHours(-settings.CancellationWindowHours);

                dto.CanCancel = isActive && !pastCutoff;

                if (!dto.CanCancel) continue;

                var price = dto.Price is null ? null : Money.Of(dto.Price.Amount, dto.Price.Currency);
                var fee = settings.CancellationPolicy.FeeFor(
                    price, dto.StartDate, now, settings.CurrencyCode);

                dto.CancellationFeeIfCancelledNow = fee is null ? null : MoneyDto.From(fee);
            }
        }
    }
}
