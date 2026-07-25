using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_AgencyId_StartDate",
                table: "Reservations");

            // A reservation's obsolete RentingState is dropped (its lifecycle is
            // now the new Status column); CarId is a fresh nullable FK, NOT a
            // rename of RentingState — the old state values must not survive as
            // bogus car ids.
            migrationBuilder.DropColumn(
                name: "RentingState",
                table: "Reservations");

            migrationBuilder.AddColumn<int>(
                name: "CarId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Reservations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Rentings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "RentingHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Payments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RentingId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesPaymentId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_AgencyId_Status_ExpiresAt",
                table: "Reservations",
                columns: new[] { "AgencyId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CarId",
                table: "Reservations",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_RentingHistories_AgencyId",
                table: "RentingHistories",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RentingId",
                table: "Payments",
                column: "RentingId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversesPaymentId",
                table: "Payments",
                column: "ReversesPaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_ReversesPaymentId",
                table: "Payments",
                column: "ReversesPaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Rentings_RentingId",
                table: "Payments",
                column: "RentingId",
                principalTable: "Rentings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RentingHistories_Agencies_AgencyId",
                table: "RentingHistories",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Cars_CarId",
                table: "Reservations",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Rentings_RentingId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RentingHistories_Agencies_AgencyId",
                table: "RentingHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Cars_CarId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_AgencyId_Status_ExpiresAt",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_CarId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_RentingHistories_AgencyId",
                table: "RentingHistories");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RentingId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "RentingHistories");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RentingId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "Reservations");

            migrationBuilder.AddColumn<int>(
                name: "RentingState",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Reservations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_AgencyId_StartDate",
                table: "Reservations",
                columns: new[] { "AgencyId", "StartDate" });
        }
    }
}
