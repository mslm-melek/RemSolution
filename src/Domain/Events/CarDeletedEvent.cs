namespace RemSolution.Domain.Events;

public class CarDeletedEvent : BaseEvent
{
    public CarDeletedEvent(Car item)
    {
        Item = item;
    }

    public Car Item { get; }
}
