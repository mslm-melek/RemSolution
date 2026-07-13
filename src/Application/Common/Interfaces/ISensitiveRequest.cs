namespace RemSolution.Application.Common.Interfaces;

// Marker for requests carrying personal or regulated data (identity-document
// numbers, birth data, file contents). Pipeline behaviours must never
// destructure these into logs — they log the request name only.
public interface ISensitiveRequest
{
}
