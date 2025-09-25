namespace RemSolution.Domain.Events;

public class ModelCarCompletedEvent : BaseEvent
{
    public ModelCarCompletedEvent(ModelCar item)
    {
        Item = item;
    }

    public ModelCar Item { get; }
}
