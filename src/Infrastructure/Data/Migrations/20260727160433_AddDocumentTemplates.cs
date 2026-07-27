using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentTemplateId",
                table: "Factures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "Factures",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentTemplateId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "Contracts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BlocksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTemplateFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    DocumentTemplateId = table.Column<int>(type: "int", nullable: false),
                    Placeholder = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Binding = table.Column<int>(type: "int", nullable: false),
                    DataPath = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FixedValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplateFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplateFields_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTemplateFields_DocumentTemplates_DocumentTemplateId",
                        column: x => x.DocumentTemplateId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Factures_DocumentTemplateId",
                table: "Factures",
                column: "DocumentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_DocumentTemplateId",
                table: "Contracts",
                column: "DocumentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplateFields_AgencyId_DocumentTemplateId",
                table: "DocumentTemplateFields",
                columns: new[] { "AgencyId", "DocumentTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplateFields_DocumentTemplateId_Placeholder",
                table: "DocumentTemplateFields",
                columns: new[] { "DocumentTemplateId", "Placeholder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_AgencyId_Kind_Language",
                table: "DocumentTemplates",
                columns: new[] { "AgencyId", "Kind", "Language" });

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_DocumentTemplates_DocumentTemplateId",
                table: "Contracts",
                column: "DocumentTemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Factures_DocumentTemplates_DocumentTemplateId",
                table: "Factures",
                column: "DocumentTemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_DocumentTemplates_DocumentTemplateId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Factures_DocumentTemplates_DocumentTemplateId",
                table: "Factures");

            migrationBuilder.DropTable(
                name: "DocumentTemplateFields");

            migrationBuilder.DropTable(
                name: "DocumentTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Factures_DocumentTemplateId",
                table: "Factures");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_DocumentTemplateId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocumentTemplateId",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "DocumentTemplateId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "Contracts");
        }
    }
}
