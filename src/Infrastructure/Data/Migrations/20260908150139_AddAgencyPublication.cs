using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Agencies",
                type: "datetime2",
                nullable: true);

            // EVERY EXISTING AGENCY IS ALREADY LIVE. Publication is new; the
            // agencies that predate it have been on the marketplace all along,
            // and leaving this null would take every one of them out of the
            // public search the moment this migration runs.
            //
            // Dated from when the agency was created rather than from the
            // deployment: that is when it actually became reachable. CreatedOn is
            // a datetimeoffset and PublishedAt is UTC, hence the shift; rows
            // predating the audit columns fall back to now.
            migrationBuilder.Sql(@"
                UPDATE [Agencies]
                SET [PublishedAt] = COALESCE(
                        CAST(SWITCHOFFSET([CreatedOn], 0) AS datetime2),
                        SYSUTCDATETIME())
                WHERE [PublishedAt] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Agencies");
        }
    }
}
