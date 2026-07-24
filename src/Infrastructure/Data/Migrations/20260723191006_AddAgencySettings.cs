using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgencySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    CancellationWindowHours = table.Column<int>(type: "int", nullable: false),
                    ReservationExpiryHours = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgencySettings_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgencySettings_AgencyId",
                table: "AgencySettings",
                column: "AgencyId",
                unique: true);

            // Backfill one settings row per existing agency, carrying its
            // current currency and the default windows, BEFORE dropping the
            // Agencies.Currency column that currency came from.
            migrationBuilder.Sql(@"
INSERT INTO [AgencySettings] ([AgencyId], [CurrencyCode], [CancellationWindowHours], [ReservationExpiryHours])
SELECT [Id], [Currency], 24, 48 FROM [Agencies];");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Agencies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Agencies",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            // Restore each agency's currency from its settings row before the
            // table goes away.
            migrationBuilder.Sql(@"
UPDATE a SET a.[Currency] = s.[CurrencyCode]
FROM [Agencies] a INNER JOIN [AgencySettings] s ON s.[AgencyId] = a.[Id];");

            migrationBuilder.DropTable(
                name: "AgencySettings");
        }
    }
}
