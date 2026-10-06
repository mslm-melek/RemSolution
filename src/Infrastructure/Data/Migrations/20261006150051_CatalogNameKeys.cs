using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogNameKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A catalog name is now a translation key, translated by the SPA and
            // the resx. The standard entries the platform has shipped so far get
            // their key, here and on every copy the agency has not changed; any
            // other name stays as typed, and reads as such in every language.
            // The typed English/Arabic names are dropped, by decision.
            migrationBuilder.Sql(@"
DECLARE @Keys TABLE (OldName nvarchar(200) COLLATE Latin1_General_100_CI_AI, NewName nvarchar(200));
INSERT INTO @Keys VALUES
    (N'Vidange', N'oilChange'), (N'Assurance', N'insurance'), (N'Vignette', N'roadTax'),
    (N'Pneus', N'tyres'), (N'Lavage', N'carWash'), (N'Réparation', N'repair');

UPDATE t SET Name = k.NewName
FROM ExpenseTypeTemplates t JOIN @Keys k ON k.OldName = t.Name;

UPDATE c SET Name = t.Name
FROM ExpenseTypes c JOIN ExpenseTypeTemplates t ON t.Id = c.TemplateId
WHERE c.IsCustomized = 0;

DELETE FROM @Keys;
INSERT INTO @Keys VALUES
    (N'GPS', N'gps'), (N'Siège bébé', N'babySeat'), (N'Conducteur additionnel', N'additionalDriver'),
    (N'Assurance tous risques', N'comprehensiveInsurance'), (N'Wifi portable', N'portableWifi');

UPDATE t SET Name = k.NewName
FROM ExtraServicesTypeTemplates t JOIN @Keys k ON k.OldName = t.Name;

UPDATE c SET Name = t.Name
FROM ExtraServicesTypes c JOIN ExtraServicesTypeTemplates t ON t.Id = c.TemplateId
WHERE c.IsCustomized = 0;");

            // The expense-due wording now reads {{expenseType}} (the suffix tells
            // the renderers to translate it); existing alerts keep their text.
            migrationBuilder.Sql(@"
UPDATE Notifications SET ArgsJson = REPLACE(ArgsJson, N'""type"":', N'""expenseType"":')
WHERE MessageKey LIKE N'carExpense%' AND ArgsJson LIKE N'%""type"":%';");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "ExtraServicesTypeTemplates");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExtraServicesTypeTemplates");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExtraServicesTypes");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "ExpenseTypeTemplates");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExpenseTypeTemplates");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExpenseTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "ExtraServicesTypeTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ExtraServicesTypeTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

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

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "ExpenseTypeTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ExpenseTypeTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

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
        }
    }
}
