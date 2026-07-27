namespace RemSolution.Domain.Enums;

// How a placeholder found in a template gets its value.
public enum DocumentFieldBinding
{
    // Read from the booking's data at generation time, via a path from
    // DocumentPlaceholders (e.g. "client.fullName"). Bound automatically when the
    // placeholder name is itself a known path.
    DataField = 0,

    // The same text on every document (a franchise amount, a branch name) —
    // stored once on the template.
    FixedValue = 1,

    // No sensible automatic source: the agent is prompted for it each time a
    // document is generated. This is the escape hatch for the clauses that vary
    // per rental and live in nobody's database column.
    AskEachTime = 2,

    // Left as a ruled blank to complete by hand on the printed copy.
    Blank = 3
}
