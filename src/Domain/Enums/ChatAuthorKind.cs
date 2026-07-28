namespace RemSolution.Domain.Enums;

/// <summary>
/// Which side of a renting's conversation wrote a message. A thread only ever
/// has these two participants — the agency's desk (any staff member with the
/// chat permission answers as the agency) and the client on the renting — so the
/// UI can align and style a message from this alone, without resolving the
/// sender's identity.
/// </summary>
public enum ChatAuthorKind
{
    Agency = 0,
    Client = 1,
}
