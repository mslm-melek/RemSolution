using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxUnavailabilityAndDocumentExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The single-column FK indexes are replaced, not lost: the new
            // IX_*_CarId_Dates below lead with CarId, so every lookup these
            // served is still a seek. Dropping them is safe under the
            // additive-migration rule in docs/RUNBOOK_Base_De_Donnees.md — the
            // previous build references index names nowhere, and keeping both
            // would pay for two indexes on every booking write.
            migrationBuilder.DropIndex(
                name: "IX_Reservations_CarId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Rentings_CarId",
                table: "Rentings");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositRetainedAmount",
                table: "Rentings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositRetainedAmountCurrency",
                table: "Rentings",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositSettledAt",
                table: "Rentings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FiscalStampAmount",
                table: "Factures",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalStampAmountCurrency",
                table: "Factures",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "Factures",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetAmountCurrency",
                table: "Factures",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentifier",
                table: "Factures",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDue",
                table: "Factures",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotalDueCurrency",
                table: "Factures",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Factures",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatAmountCurrency",
                table: "Factures",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRatePercent",
                table: "Factures",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CINExpiryDate",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DrivingLicenceExpiryDate",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasseportExpiryDate",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientDocumentExpiryLeadDays",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FiscalStampAmount",
                table: "AgencySettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentifier",
                table: "AgencySettings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRatePercent",
                table: "AgencySettings",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            // A non-nullable column added to an existing table lands on 0 for
            // every row already there, and the AgencySettings defaults above are
            // C#-side — so without this, every existing agency would silently get
            // a 0 % VAT rate and stop printing a tax breakdown, which is the
            // exact legal defect these columns were added to fix. New agencies
            // take the same figures from the entity's own defaults.
            //
            // Deliberately NOT applied to Factures: an invoice issued before this
            // migration really was issued without a VAT breakdown, and back-dating
            // one onto it would make the stored document disagree with the PDF the
            // client holds.
            migrationBuilder.Sql(@"
UPDATE dbo.AgencySettings
SET VatRatePercent = 19.00,
    FiscalStampAmount = 1.00,
    ClientDocumentExpiryLeadDays = 30
WHERE VatRatePercent = 0
  AND FiscalStampAmount = 0
  AND ClientDocumentExpiryLeadDays = 0;");

            migrationBuilder.CreateTable(
                name: "CarUnavailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    CarId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarUnavailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarUnavailabilities_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CarUnavailabilities_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CarId_Dates",
                table: "Reservations",
                columns: new[] { "CarId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "Status", "AgencyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rentings_CarId_Dates",
                table: "Rentings",
                columns: new[] { "CarId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "RentingState", "AgencyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CarUnavailabilities_AgencyId_CarId",
                table: "CarUnavailabilities",
                columns: new[] { "AgencyId", "CarId" });

            migrationBuilder.CreateIndex(
                name: "IX_CarUnavailabilities_CarId_Dates",
                table: "CarUnavailabilities",
                columns: new[] { "CarId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "Reason", "AgencyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarUnavailabilities");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_CarId_Dates",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Rentings_CarId_Dates",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "DepositRetainedAmount",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "DepositRetainedAmountCurrency",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "DepositSettledAt",
                table: "Rentings");

            migrationBuilder.DropColumn(
                name: "FiscalStampAmount",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "FiscalStampAmountCurrency",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "NetAmountCurrency",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "TaxIdentifier",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "TotalDue",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "TotalDueCurrency",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "VatAmountCurrency",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "VatRatePercent",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "CINExpiryDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceExpiryDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PasseportExpiryDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ClientDocumentExpiryLeadDays",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "FiscalStampAmount",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "TaxIdentifier",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "VatRatePercent",
                table: "AgencySettings");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CarId",
                table: "Reservations",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentings_CarId",
                table: "Rentings",
                column: "CarId");
        }
    }
}
