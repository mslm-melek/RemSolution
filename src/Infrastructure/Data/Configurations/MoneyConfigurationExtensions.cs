using System.Linq.Expressions;
using RemSolution.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public static class MoneyConfigurationExtensions
{
    /// <summary>
    /// Maps a nullable <see cref="Money"/> property as an optional owned type:
    /// the amount keeps <paramref name="amountColumn"/> (decimal(18,2)) — the
    /// original column name, so existing amounts survive the migration — and the
    /// currency lands in a new <paramref name="currencyColumn"/> (char(3)). Both
    /// columns are null together when the money is absent; both are set together
    /// when present (enforced by the Money invariant and the backfill migration).
    /// </summary>
    public static EntityTypeBuilder<TEntity> OwnsMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money?>> navigation,
        string amountColumn,
        string currencyColumn)
        where TEntity : class
    {
        builder.OwnsOne(navigation, money =>
        {
            money.Property(m => m.Amount)
                 .HasColumnName(amountColumn)
                 .HasColumnType("decimal(18,2)");

            money.Property(m => m.Currency)
                 .HasColumnName(currencyColumn)
                 .HasMaxLength(3)
                 .IsUnicode(false);
        });

        // Optional: the owned reference is absent for rows with no amount.
        builder.Navigation(navigation).IsRequired(false);

        return builder;
    }
}
