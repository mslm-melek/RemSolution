using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientDocumentValidityYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The defaults are written here, not left to the C# initialisers:
            // an added non-nullable column lands on the SQL default for rows that
            // already exist, and zero means "does not expire" — which would leave
            // every agency created before this migration deriving nothing.
            migrationBuilder.AddColumn<int>(
                name: "CINValidityYears",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "DrivingLicenceValidityYears",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "PasseportValidityYears",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CINValidityYears",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceValidityYears",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "PasseportValidityYears",
                table: "AgencySettings");
        }
    }
}
