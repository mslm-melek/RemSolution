using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyReportThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive only, nothing to backfill. MessageCount lands on 0 for every
            // existing report, which is the truth — the thread table is new. The
            // reopening date is null (never reopened), and SQL Server fills a
            // rowversion column for existing rows itself. The new
            // AgencyReportStatus.Withdrawn (4) is a value in an existing int
            // column, so the old version simply never writes it.
            migrationBuilder.AddColumn<int>(
                name: "MessageCount",
                table: "AgencyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAt",
                table: "AgencyReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AgencyReports",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgencyReportMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyReportId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FromPlatform = table.Column<bool>(type: "bit", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencyReportMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgencyReportMessages_AgencyReports_AgencyReportId",
                        column: x => x.AgencyReportId,
                        principalTable: "AgencyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgencyReportMessages_AgencyReportId_SentAt",
                table: "AgencyReportMessages",
                columns: new[] { "AgencyReportId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgencyReportMessages");

            migrationBuilder.DropColumn(
                name: "MessageCount",
                table: "AgencyReports");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                table: "AgencyReports");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AgencyReports");
        }
    }
}
