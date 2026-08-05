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
    ExpenseFacture = 4,
    // Documents the system GENERATES rather than receives: the rental agreement
    // and the client invoice rendered for a renting. Same StoredFile plumbing
    // (hash, dedup, size) so a generated PDF is archived like any other file.
    RentalContract = 5,
    RentalFacture = 6,
    // Proof kept against a payment entry: a receipt, a transfer slip, or the
    // supplier invoice behind it. Distinct from ExpenseFacture, which is the
    // invoice attached to the expense record itself.
    PaymentProof = 7,
    // The client's head, cut out of their CIN image and squared off — the face
    // shown beside their name in lists. DERIVED, not received: it is generated
    // from the CIN file (see IPortraitCropper) and regenerated whenever that
    // file is replaced, so it is never the only copy of anything.
    ClientPortrait = 8
}
