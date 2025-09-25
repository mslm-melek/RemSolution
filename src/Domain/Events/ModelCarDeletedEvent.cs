namespace RemSolution.Domain.Events;

public class ModelCarDeletedEvent : BaseEvent
{
    public ModelCarDeletedEvent(ModelCar item)
    {
        Item = item;
    }

    public ModelCar Item { get; }
}
