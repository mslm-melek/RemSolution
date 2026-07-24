namespace RemSolution.Domain.Enums;

/// <summary>
/// Operational availability of a car. Only <see cref="Active"/> cars are
/// bookable; availability/search excludes the others. New cars default to
/// <see cref="Active"/>.
/// </summary>
public enum CarStatus
{
    Active = 0,
    Maintenance = 1,
    Inactive = 2,
}
