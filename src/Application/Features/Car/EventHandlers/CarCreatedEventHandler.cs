using Microsoft.Extensions.Logging;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.Car.EventHandlers
{
   
    public class CarCreatedEventHandler : INotificationHandler<CarCompletedEvent>
    {
        private readonly ILogger<CarCreatedEventHandler> _logger;

        public CarCreatedEventHandler(ILogger<CarCreatedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(CarCompletedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Car created: {CarId}, Model: {Model}", notification.Item.Matricule, notification.Item.Model);
            return Task.CompletedTask;
        }

    }
}
