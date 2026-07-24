using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeAgencyFeatureOnAgencyDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgencyFeatures_Agencies_AgencyId",
                table: "AgencyFeatures");

            migrationBuilder.AddForeignKey(
                name: "FK_AgencyFeatures_Agencies_AgencyId",
                table: "AgencyFeatures",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgencyFeatures_Agencies_AgencyId",
                table: "AgencyFeatures");

            migrationBuilder.AddForeignKey(
                name: "FK_AgencyFeatures_Agencies_AgencyId",
                table: "AgencyFeatures",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
