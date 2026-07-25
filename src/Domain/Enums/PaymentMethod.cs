namespace RemSolution.Domain.Enums;

/// <summary>
/// How a <see cref="Entities.Payment"/> was tendered. No billing provider yet,
/// so this is recorded manually by staff.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Transfer = 2,
    Cheque = 3,
}
