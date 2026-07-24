namespace RemSolution.Domain.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.CarImage"/>'s derivative generation. The
/// original is stored synchronously on upload (status <see cref="Pending"/>);
/// the background pipeline moves it to <see cref="Processing"/>, then
/// <see cref="Completed"/> once the thumbnail and medium exist, or
/// <see cref="Failed"/> if generation threw (the original is still usable).
/// </summary>
public enum ImageProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}
