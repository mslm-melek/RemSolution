using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyIdToTenantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Rentings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "ExtraServices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Cars",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_AgencyId_StartDate",
                table: "Reservations",
                columns: new[] { "AgencyId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Rentings_AgencyId_RentingState",
                table: "Rentings",
                columns: new[] { "AgencyId", "RentingState" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_AgencyId_PayementDate",
                table: "Payments",
                columns: new[] { "AgencyId", "PayementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraServices_AgencyId",
                table: "ExtraServices",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_AgencyId_ExpenseDate",
                table: "Expenses",
                columns: new[] { "AgencyId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AgencyId",
                table: "Clients",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_AgencyId_ModelId",
                table: "Cars",
                columns: new[] { "AgencyId", "ModelId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_Agencies_AgencyId",
                table: "Cars",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Agencies_AgencyId",
                table: "Clients",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Agencies_AgencyId",
                table: "Expenses",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExtraServices_Agencies_AgencyId",
                table: "ExtraServices",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Agencies_AgencyId",
                table: "Payments",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentings_Agencies_AgencyId",
                table: "Rentings",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Agencies_AgencyId",
                table: "Reservations",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cars_Agencies_AgencyId",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Agencies_AgencyId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Agencies_AgencyId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_ExtraServices_Agencies_AgencyId",
                table: "ExtraServices");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Agencies_AgencyId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentings_Agencies_AgencyId",
                table: "Rentings");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Agencies_AgencyId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_AgencyId_StartDate",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Rentings_AgencyId_RentingState",
                table: "Rentings");

            migrationBuilder.DropIndex(
                name: "IX_Payments_AgencyId_PayementDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_ExtraServices_AgencyId",
                table: "ExtraServices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_AgencyId_ExpenseDate",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Clients_AgencyId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Cars_AgencyId_ModelId",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "ExtraServices");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Cars");
        }
    }
}
