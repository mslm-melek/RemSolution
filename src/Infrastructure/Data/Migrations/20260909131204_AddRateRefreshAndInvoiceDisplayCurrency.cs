using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRateRefreshAndInvoiceDisplayCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No backfill on any of these, each for a stated reason rather than
            // by omission (see the additive-migration rule in
            // docs/RUNBOOK_Base_De_Donnees.md).
            //
            // The three Factures columns stay NULL on every invoice already
            // issued, and that is exactly right: none of them carried a
            // converted total, so none should start claiming one. Note the 18,6
            // scale — it matches ExchangeRates.Rate, because a frozen copy at
            // 18,2 would round 0.296247 to 0.30 and reproduce nothing.
            //
            // ExchangeRates.IsPinned lands on false, which is both the C# default
            // and the wanted behaviour: the pairs quoted so far become the ones
            // the nightly refresh maintains, which is the point of adding it.
            // RefreshedAt stays NULL, meaning "last written by a person" — true
            // of every existing row.
            //
            // AgencySettings.InvoiceDisplayCurrency stays NULL, so no agency
            // starts printing a second currency it never asked for.
            migrationBuilder.AddColumn<string>(
                name: "DisplayCurrency",
                table: "Factures",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DisplayExchangeRate",
                table: "Factures",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisplayRateAsOf",
                table: "Factures",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "ExchangeRates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefreshedAt",
                table: "ExchangeRates",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceDisplayCurrency",
                table: "AgencySettings",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayCurrency",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "DisplayExchangeRate",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "DisplayRateAsOf",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "RefreshedAt",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "InvoiceDisplayCurrency",
                table: "AgencySettings");
        }
    }
}
