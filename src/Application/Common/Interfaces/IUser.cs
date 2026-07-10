namespace RemSolution.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }

    /// <summary>
    /// Human-readable username from the claims principal (no database
    /// round-trip). Logged and snapshotted on audit rows so "who did it"
    /// stays answerable even if the account is later renamed or deleted.
    /// </summary>
    string? UserName { get; }
}
