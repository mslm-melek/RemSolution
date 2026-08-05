using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPortrait : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CINPortraitFileId",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CINPortraitFileId",
                table: "Clients",
                column: "CINPortraitFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_StoredFiles_CINPortraitFileId",
                table: "Clients",
                column: "CINPortraitFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_StoredFiles_CINPortraitFileId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CINPortraitFileId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CINPortraitFileId",
                table: "Clients");
        }
    }
}
