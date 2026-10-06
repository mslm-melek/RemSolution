using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Expense and add-on types move from one global catalog to one catalog per
    /// agency. Each existing global type becomes a platform template (keeping its
    /// id), every agency gets its own copy of it, and every expense, car schedule
    /// and extra service is re-pointed at its own agency's copy. An add-on's old
    /// bare price lands on each copy in that agency's currency, which is how it
    /// was already being read.
    /// <para>
    /// Not additive, and cannot be: AgencyId ends up NOT NULL. While the previous
    /// version is still serving, creating a type fails and the type lists show
    /// every agency's copies; nothing else it does touches these tables.
    /// </para>
    /// </summary>
    public partial class PerAgencyCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable first: the old global rows have no agency until they are
            // moved out below.
            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "ExtraServicesTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmountCurrency",
                table: "ExtraServicesTypes",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                table: "ExtraServicesTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "ExtraServicesTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ExtraServicesTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "ExtraServicesTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "ExpenseTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                table: "ExpenseTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "ExpenseTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ExpenseTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "ExpenseTypes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseTypeTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    WithNotif = table.Column<bool>(type: "bit", nullable: false),
                    AfterKilometer = table.Column<int>(type: "int", nullable: true),
                    AfterMonth = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTypeTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtraServicesTypeTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraServicesTypeTemplates", x => x.Id);
                });

            // 1. The global rows become the templates, ids kept so step 2 can join on them.
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT ExpenseTypeTemplates ON;
INSERT INTO ExpenseTypeTemplates (Id, Name, IsActive, WithNotif, AfterKilometer, AfterMonth, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
SELECT Id, ISNULL(Name, N''), IsActive, WithNotif, AfterKilometer, AfterMonth, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
FROM ExpenseTypes;
SET IDENTITY_INSERT ExpenseTypeTemplates OFF;

SET IDENTITY_INSERT ExtraServicesTypeTemplates ON;
INSERT INTO ExtraServicesTypeTemplates (Id, Name, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
SELECT Id, ISNULL(Name, N''), IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
FROM ExtraServicesTypes;
SET IDENTITY_INSERT ExtraServicesTypeTemplates OFF;");

            // 2. A copy per agency, switched on or off as the global row was.
            migrationBuilder.Sql(@"
INSERT INTO ExpenseTypes (AgencyId, TemplateId, IsCustomized, Name, IsActive, WithNotif, AfterKilometer, AfterMonth, CreatedBy, CreatedOn)
SELECT a.Id, t.Id, 0, t.Name, t.IsActive, t.WithNotif, t.AfterKilometer, t.AfterMonth, t.CreatedBy, t.CreatedOn
FROM ExpenseTypes t CROSS JOIN Agencies a
WHERE t.AgencyId IS NULL;

INSERT INTO ExtraServicesTypes (AgencyId, TemplateId, IsCustomized, Name, Amount, AmountCurrency, IsActive, CreatedBy, CreatedOn)
SELECT a.Id, t.Id, 0, t.Name,
       CASE WHEN s.CurrencyCode IS NULL THEN NULL ELSE t.Amount END,
       CASE WHEN t.Amount IS NULL THEN NULL ELSE s.CurrencyCode END,
       t.IsActive, t.CreatedBy, t.CreatedOn
FROM ExtraServicesTypes t CROSS JOIN Agencies a
LEFT JOIN AgencySettings s ON s.AgencyId = a.Id
WHERE t.AgencyId IS NULL;");

            // 3. Everything that named a global row now names its agency's copy.
            migrationBuilder.Sql(@"
UPDATE e SET ExpenseTypeId = c.Id
FROM Expenses e JOIN ExpenseTypes c ON c.TemplateId = e.ExpenseTypeId AND c.AgencyId = e.AgencyId;

UPDATE s SET ExpenseTypeId = c.Id
FROM CarExpenseSchedules s JOIN ExpenseTypes c ON c.TemplateId = s.ExpenseTypeId AND c.AgencyId = s.AgencyId;

UPDATE x SET ExtraServicesTypeId = c.Id
FROM ExtraServices x JOIN ExtraServicesTypes c ON c.TemplateId = x.ExtraServicesTypeId AND c.AgencyId = x.AgencyId;");

            // 4. Nothing points at the global rows any more.
            migrationBuilder.Sql(@"
DELETE FROM ExpenseTypes WHERE AgencyId IS NULL;
DELETE FROM ExtraServicesTypes WHERE AgencyId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "AgencyId",
                table: "ExpenseTypes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AgencyId",
                table: "ExtraServicesTypes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtraServicesTypes_AgencyId_TemplateId",
                table: "ExtraServicesTypes",
                columns: new[] { "AgencyId", "TemplateId" },
                unique: true,
                filter: "[TemplateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraServicesTypes_TemplateId",
                table: "ExtraServicesTypes",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_AgencyId_TemplateId",
                table: "ExpenseTypes",
                columns: new[] { "AgencyId", "TemplateId" },
                unique: true,
                filter: "[TemplateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_TemplateId",
                table: "ExpenseTypes",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseTypes_Agencies_AgencyId",
                table: "ExpenseTypes",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseTypes_ExpenseTypeTemplates_TemplateId",
                table: "ExpenseTypes",
                column: "TemplateId",
                principalTable: "ExpenseTypeTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExtraServicesTypes_Agencies_AgencyId",
                table: "ExtraServicesTypes",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExtraServicesTypes_ExtraServicesTypeTemplates_TemplateId",
                table: "ExtraServicesTypes",
                column: "TemplateId",
                principalTable: "ExtraServicesTypeTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Structural only: every agency's copies stay behind as global rows, so
        /// the old catalog lists each type once per agency. References stay valid.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseTypes_Agencies_AgencyId",
                table: "ExpenseTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseTypes_ExpenseTypeTemplates_TemplateId",
                table: "ExpenseTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ExtraServicesTypes_Agencies_AgencyId",
                table: "ExtraServicesTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ExtraServicesTypes_ExtraServicesTypeTemplates_TemplateId",
                table: "ExtraServicesTypes");

            migrationBuilder.DropTable(
                name: "ExpenseTypeTemplates");

            migrationBuilder.DropTable(
                name: "ExtraServicesTypeTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ExtraServicesTypes_AgencyId_TemplateId",
                table: "ExtraServicesTypes");

            migrationBuilder.DropIndex(
                name: "IX_ExtraServicesTypes_TemplateId",
                table: "ExtraServicesTypes");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseTypes_AgencyId_TemplateId",
                table: "ExpenseTypes");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseTypes_TemplateId",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "AmountCurrency",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "IsCustomized",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "IsCustomized",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "ExpenseTypes");
        }
    }
}
