using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deliberately NOT backfilled. Nothing on an already-cancelled row
            // records whether it had been confirmed first, so 0 is the only
            // honest answer — and it is also the lenient one: no agency is
            // penalised retroactively for a cancellation nobody can reconstruct.
            migrationBuilder.AddColumn<bool>(
                name: "CancelledAfterConfirmation",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AgencyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    ReservationId = table.Column<int>(type: "int", nullable: true),
                    RentingId = table.Column<int>(type: "int", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReporterName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BookingSummary = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AgencyCancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencyReports", x => x.Id);
                    table.CheckConstraint("CK_AgencyReports_OneBooking", "(CASE WHEN [ReservationId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [RentingId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_AgencyReports_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgencyReports_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgencyReports_Rentings_RentingId",
                        column: x => x.RentingId,
                        principalTable: "Rentings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgencyReports_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgencyReports_AgencyId_SubmittedAt",
                table: "AgencyReports",
                columns: new[] { "AgencyId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgencyReports_ClientId",
                table: "AgencyReports",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AgencyReports_RentingId",
                table: "AgencyReports",
                column: "RentingId",
                unique: true,
                filter: "[RentingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgencyReports_ReservationId",
                table: "AgencyReports",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgencyReports_Status_SubmittedAt",
                table: "AgencyReports",
                columns: new[] { "Status", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgencyReports");

            migrationBuilder.DropColumn(
                name: "CancelledAfterConfirmation",
                table: "Reservations");
        }
    }
}
