using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ReservationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelledReason",
                table: "Reservations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositAmountCurrency",
                table: "Reservations",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpiredReason",
                table: "Reservations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedReason",
                table: "Reservations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Rentings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositAmountCurrency",
                table: "Rentings",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefund",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReservationId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CancelledReason",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "DepositAmountCurrency",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ExpiredReason",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RejectedReason",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "DepositAmountCurrency",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "IsRefund",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Payments");
        }
    }
}
