using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayedPriceCurrency",
                table: "Reservations",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceCurrency",
                table: "Reservations",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceCurrency",
                table: "Rentings",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceCurrency",
                table: "RentingHistories",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayementAmountCurrency",
                table: "Payments",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotalAmountCurrency",
                table: "ExtraServices",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DailyRateCurrency",
                table: "Cars",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            // Existing agencies predate multi-currency; default them to TND
            // (the platform's launch currency). New agencies set it explicitly.
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Agencies",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            // Backfill the currency of every existing amount from its owning
            // agency's currency, so a present amount always has a matching
            // currency (Money is present) and a null amount keeps a null
            // currency (Money is absent). RentingHistories has no AgencyId, so
            // it derives the currency through its parent Renting.
            migrationBuilder.Sql(@"
UPDATE c SET c.[DailyRateCurrency] = a.[Currency]
FROM [Cars] c INNER JOIN [Agencies] a ON a.[Id] = c.[AgencyId]
WHERE c.[DailyRate] IS NOT NULL;

UPDATE r SET r.[PriceCurrency] = a.[Currency]
FROM [Rentings] r INNER JOIN [Agencies] a ON a.[Id] = r.[AgencyId]
WHERE r.[Price] IS NOT NULL;

UPDATE res SET res.[PriceCurrency] = a.[Currency]
FROM [Reservations] res INNER JOIN [Agencies] a ON a.[Id] = res.[AgencyId]
WHERE res.[Price] IS NOT NULL;

UPDATE res SET res.[PayedPriceCurrency] = a.[Currency]
FROM [Reservations] res INNER JOIN [Agencies] a ON a.[Id] = res.[AgencyId]
WHERE res.[PayedPrice] IS NOT NULL;

UPDATE p SET p.[PayementAmountCurrency] = a.[Currency]
FROM [Payments] p INNER JOIN [Agencies] a ON a.[Id] = p.[AgencyId]
WHERE p.[PayementAmount] IS NOT NULL;

UPDATE es SET es.[TotalAmountCurrency] = a.[Currency]
FROM [ExtraServices] es INNER JOIN [Agencies] a ON a.[Id] = es.[AgencyId]
WHERE es.[TotalAmount] IS NOT NULL;

UPDATE rh SET rh.[PriceCurrency] = a.[Currency]
FROM [RentingHistories] rh
INNER JOIN [Rentings] r ON r.[Id] = rh.[RentingId]
INNER JOIN [Agencies] a ON a.[Id] = r.[AgencyId]
WHERE rh.[Price] IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayedPriceCurrency",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PriceCurrency",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PriceCurrency",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "PriceCurrency",
                table: "RentingHistories");

            migrationBuilder.DropColumn(
                name: "PayementAmountCurrency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TotalAmountCurrency",
                table: "ExtraServices");

            migrationBuilder.DropColumn(
                name: "DailyRateCurrency",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Agencies");
        }
    }
}
