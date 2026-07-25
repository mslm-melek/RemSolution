using Microsoft.Extensions.Logging;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.Renting.EventHandlers
{
    // First consumer of the domain-event pipeline. The RentingHistory snapshot is
    // written transactionally by ChangeRentingStateCommand itself; this handler is
    // the seam for completion side-effects (notifications, statistics refresh)
    // that don't belong in the transaction. For now it records the event.
    public class RentingCompletedEventHandler : INotificationHandler<RentingCompletedEvent>
    {
        private readonly ILogger<RentingCompletedEventHandler> _logger;

        public RentingCompletedEventHandler(ILogger<RentingCompletedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(RentingCompletedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Renting {RentingId} completed for agency {AgencyId}",
                notification.Renting.Id, notification.Renting.AgencyId);

            return Task.CompletedTask;
        }
    }
}
