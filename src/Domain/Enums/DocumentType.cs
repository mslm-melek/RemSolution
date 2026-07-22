namespace RemSolution.Domain.Enums;

// The kind of thing a StoredFile holds. Spans every file-carrying entity, so a
// single tag identifies a file regardless of which record points at it. The
// first three values line up by name with ClientDocumentType (the client upload
// API surface), which maps onto this enum.
public enum DocumentType
{
    CIN = 0,
    DrivingLicence = 1,
    Passeport = 2,
    CarPhoto = 3,
    ExpenseFacture = 4
}
