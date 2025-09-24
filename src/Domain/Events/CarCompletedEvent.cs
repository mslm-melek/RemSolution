namespace RemSolution.Domain.Events;

public class CarCompletedEvent : BaseEvent
{
    public CarCompletedEvent(Car item)
    {
        Item = item;
    }

    public Car Item { get; }
}
