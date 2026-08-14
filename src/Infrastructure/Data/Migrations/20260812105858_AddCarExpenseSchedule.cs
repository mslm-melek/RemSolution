using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarExpenseSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarExpenseSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    CarId = table.Column<int>(type: "int", nullable: false),
                    ExpenseTypeId = table.Column<int>(type: "int", nullable: false),
                    AfterKilometer = table.Column<int>(type: "int", nullable: true),
                    AfterMonth = table.Column<int>(type: "int", nullable: true),
                    LeadKilometers = table.Column<int>(type: "int", nullable: true),
                    LeadDays = table.Column<int>(type: "int", nullable: true),
                    LastDoneMileage = table.Column<int>(type: "int", nullable: true),
                    LastDoneOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarExpenseSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarExpenseSchedules_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CarExpenseSchedules_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarExpenseSchedules_ExpenseTypes_ExpenseTypeId",
                        column: x => x.ExpenseTypeId,
                        principalTable: "ExpenseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarExpenseSchedules_AgencyId_CarId_ExpenseTypeId",
                table: "CarExpenseSchedules",
                columns: new[] { "AgencyId", "CarId", "ExpenseTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarExpenseSchedules_CarId",
                table: "CarExpenseSchedules",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_CarExpenseSchedules_ExpenseTypeId",
                table: "CarExpenseSchedules",
                column: "ExpenseTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarExpenseSchedules");
        }
    }
}
