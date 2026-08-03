using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProofFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProofFileId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProofFileId",
                table: "Payments",
                column: "ProofFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_StoredFiles_ProofFileId",
                table: "Payments",
                column: "ProofFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_StoredFiles_ProofFileId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ProofFileId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProofFileId",
                table: "Payments");
        }
    }
}
