using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalDataErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No backfill on either column, and both for a stated reason rather
            // than by omission (see the additive-migration rule in
            // docs/RUNBOOK_Base_De_Donnees.md).
            //
            // NULL is right for every existing client: nobody's data has been
            // erased yet, and that is what null means here.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PersonalDataErasedAt",
                table: "Clients",
                type: "datetimeoffset",
                nullable: true);

            // The usual trap — a non-nullable column landing on 0 rather than on
            // the C# default — is harmless here because 0 IS the C# default, and
            // it means "no automatic purge". Existing agencies therefore keep
            // exactly the behaviour they had until one of them sets a window.
            migrationBuilder.AddColumn<int>(
                name: "PersonalDataRetentionMonths",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonalDataErasedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PersonalDataRetentionMonths",
                table: "AgencySettings");
        }
    }
}
