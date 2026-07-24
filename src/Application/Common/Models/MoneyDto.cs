using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Common.Models;

/// <summary>
/// API shape of a <see cref="Money"/> amount. Mapster maps Money → MoneyDto by
/// member name; a null Money maps to a null MoneyDto.
/// </summary>
public record MoneyDto(decimal Amount, string Currency)
{
    // Parameterless-friendly overload for Mapster's projection over a possibly
    // null owned reference is unnecessary — Mapster handles the null owner —
    // but the explicit factory keeps hand construction readable.
    public static MoneyDto From(Money money) => new(money.Amount, money.Currency);
}
