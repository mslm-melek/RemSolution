using Microsoft.Extensions.Logging;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.Car.EventHandlers;

public class CarCompletedEventHandler : INotificationHandler<CarCompletedEvent>
{
    private readonly ILogger<CarCompletedEventHandler> _logger;

    public CarCompletedEventHandler(ILogger<CarCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(CarCompletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RemSolution Domain Event: {DomainEvent}", notification.GetType().Name);

        return Task.CompletedTask;
    }
}
