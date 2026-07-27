namespace RemSolution.Domain.Enums;

// Which piece of paperwork a template produces. Deliberately not the same enum
// as DocumentType: that one tags every StoredFile in the system (including car
// photos and identity papers), while this one only names the two documents an
// agency can template. The two are mapped explicitly where they meet.
public enum DocumentTemplateKind
{
    Contract = 0,
    Facture = 1
}
