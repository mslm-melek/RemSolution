using System.Linq.Expressions;

namespace RemSolution.Application.Common.Mappings;

// Column sorting for the list screens. Every sortable column is spelled out in
// the handler's own switch (see GetCarsWithPaginationQuery) rather than resolved
// by name at runtime: the client cannot ask to order by an arbitrary property,
// and each key stays a compile-checked expression EF can translate.
public static class SortingExtensions
{
    // The two halves of a sort — direction is data, the key is code.
    public static IOrderedQueryable<T> OrderByField<T, TKey>(
        this IQueryable<T> query, Expression<Func<T, TKey>> key, bool descending)
        => descending ? query.OrderByDescending(key) : query.OrderBy(key);

    public static IOrderedQueryable<T> ThenByField<T, TKey>(
        this IOrderedQueryable<T> query, Expression<Func<T, TKey>> key, bool descending)
        => descending ? query.ThenByDescending(key) : query.ThenBy(key);

    // Column keys travel as the same lower-case names the Angular tables use for
    // their matColumnDef, so the header a user clicked maps straight through.
    public static string? NormalizeSortKey(this string? sortBy)
        => string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim().ToLowerInvariant();
}
