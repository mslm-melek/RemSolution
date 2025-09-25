using Microsoft.Extensions.Logging;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.ModelCar.EventHandlers;

public class ModelCarCompletedEventHandler : INotificationHandler<ModelCarCompletedEvent>
{
    private readonly ILogger<ModelCarCompletedEventHandler> _logger;

    public ModelCarCompletedEventHandler(ILogger<ModelCarCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ModelCarCompletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RemSolution Domain Event: {DomainEvent}", notification.GetType().Name);

        return Task.CompletedTask;
    }
}
