using Microsoft.Extensions.Logging;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.ModelCar.EventHandlers
{
   
    public class ModelCarCreatedEventHandler : INotificationHandler<ModelCarCompletedEvent>
    {
        private readonly ILogger<ModelCarCreatedEventHandler> _logger;

        public ModelCarCreatedEventHandler(ILogger<ModelCarCreatedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(ModelCarCompletedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ModelCar created: {ModelCarId}, Model: {Model} for brand : {Brand}", notification.Item.Id, notification.Item.Name, notification.Item.Brand?.Name );
            return Task.CompletedTask;
        }

    }
}
