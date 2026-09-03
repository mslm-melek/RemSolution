using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationPolicyAndReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CancellationFee",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationFeeCurrency",
                table: "Reservations",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancelledByCustomer",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CancellationFeeMode",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CancellationFeeValue",
                table: "AgencySettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // 48, not the 0 an added column would otherwise land on for existing
            // rows: zero would read as "nothing is ever free", which is the
            // opposite of what an agency that has not opted into fees means.
            // Harmless while CancellationFeeMode is None, wrong the moment it
            // is not.
            migrationBuilder.AddColumn<int>(
                name: "CancellationFreeHours",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 48);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationFee",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CancellationFeeCurrency",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CancelledByCustomer",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CancellationFeeMode",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "CancellationFeeValue",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "CancellationFreeHours",
                table: "AgencySettings");
        }
    }
}
