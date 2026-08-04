using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mileage",
                table: "Expenses",
                type: "int",
                nullable: true);

            // Every default below is the AgencySettings property initializer, not
            // the CLR zero EF scaffolds. A lead time of 0 means "do not warn me"
            // and NotifyStaffByEmail false means "no mail", so taking the
            // scaffolded values would hand every existing agency a notifications
            // module that is switched off — and look like the feature not working
            // rather than like a setting nobody chose.
            //
            // The one exception is NotifyClientsByEmail, which is false by design:
            // an agency opts in to writing to its customers.
            migrationBuilder.AddColumn<int>(
                name: "ClientReminderDaysBeforeEnd",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ClientReminderDaysBeforeStart",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseDueLeadDays",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseDueLeadKilometers",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyClientsByEmail",
                table: "AgencySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyStaffByEmail",
                table: "AgencySettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservationUpcomingLeadDays",
                table: "AgencySettings",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    ArgsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DedupKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AgencyId_DedupKey",
                table: "Notifications",
                columns: new[] { "AgencyId", "DedupKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AgencyId_RecipientUserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "AgencyId", "RecipientUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AgencyId_RecipientUserId_ReadAt",
                table: "Notifications",
                columns: new[] { "AgencyId", "RecipientUserId", "ReadAt" },
                filter: "[ReadAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ClientId",
                table: "Notifications",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropColumn(
                name: "Mileage",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ClientReminderDaysBeforeEnd",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "ClientReminderDaysBeforeStart",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "ExpenseDueLeadDays",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "ExpenseDueLeadKilometers",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "NotifyClientsByEmail",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "NotifyStaffByEmail",
                table: "AgencySettings");

            migrationBuilder.DropColumn(
                name: "ReservationUpcomingLeadDays",
                table: "AgencySettings");
        }
    }
}
