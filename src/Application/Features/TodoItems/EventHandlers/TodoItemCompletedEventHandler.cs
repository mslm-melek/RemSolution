using RemSolution.Domain.Events;
using Microsoft.Extensions.Logging;

namespace RemSolution.Application.Features.TodoItems.EventHandlers;

public class CarCompletedEventHandler : INotificationHandler<TodoItemCompletedEvent>
{
    private readonly ILogger<CarCompletedEventHandler> _logger;

    public CarCompletedEventHandler(ILogger<CarCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TodoItemCompletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RemSolution Domain Event: {DomainEvent}", notification.GetType().Name);

        return Task.CompletedTask;
    }
}
