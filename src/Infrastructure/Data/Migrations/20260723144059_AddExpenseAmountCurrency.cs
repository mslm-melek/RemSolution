using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseAmountCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpenseAmountCurrency",
                table: "Expenses",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            // Backfill each existing amount's currency from its owning agency
            // (Expense is tenant-scoped via AgencyId), so a present amount always
            // carries a currency (Money is present) and a null amount keeps a
            // null currency (Money is absent). Mirrors AddMoneyCurrency.
            migrationBuilder.Sql(@"
UPDATE e SET e.[ExpenseAmountCurrency] = a.[Currency]
FROM [Expenses] e INNER JOIN [Agencies] a ON a.[Id] = e.[AgencyId]
WHERE e.[ExpenseAmount] IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpenseAmountCurrency",
                table: "Expenses");
        }
    }
}
