namespace RemSolution.Domain.Events;

/// <summary>
/// Raised when a renting transitions to <see cref="Enums.RentingState.Done"/>.
/// Consumed by the renting-completion handler, which writes a
/// <see cref="RentingHistory"/> snapshot of the finished period. This is the
/// first real consumer of the domain-event pipeline.
/// </summary>
public class RentingCompletedEvent : BaseEvent
{
    public RentingCompletedEvent(Renting renting)
    {
        Renting = renting;
    }

    public Renting Renting { get; }
}
