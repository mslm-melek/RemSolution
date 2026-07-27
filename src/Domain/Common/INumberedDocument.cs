namespace RemSolution.Domain.Common;

/// <summary>
/// A legal document that carries a per-agency, per-year sequential number
/// (contracts, invoices). The sequence is scoped to the agency AND the year, so
/// two agencies never share a number and each year restarts at 1 — the shape
/// accountants and auditors expect.
/// <para>
/// <see cref="SequenceNumber"/> is the machine-comparable counter and the thing
/// the unique index is built on; <see cref="Number"/> is the rendered form
/// printed on the document. Both are assigned once, at creation, under the
/// per-agency write lock (a bare MAX+1 without the lock is a race), and are
/// never rewritten afterwards — a document's number is part of its identity.
/// </para>
/// </summary>
public interface INumberedDocument
{
    /// <summary>Calendar year (UTC) the sequence belongs to.</summary>
    int Year { get; set; }

    /// <summary>1-based counter within (agency, year).</summary>
    int SequenceNumber { get; set; }

    /// <summary>Rendered number as printed, e.g. "CTR-2026-000042".</summary>
    string Number { get; set; }
}
