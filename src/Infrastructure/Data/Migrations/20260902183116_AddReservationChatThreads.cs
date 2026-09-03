using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RentingId",
                table: "ChatMessages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ReservationId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_AgencyId_ReservationId_SentAt",
                table: "ChatMessages",
                columns: new[] { "AgencyId", "ReservationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ReservationId",
                table: "ChatMessages",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Reservations_ReservationId",
                table: "ChatMessages",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Reservations_ReservationId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_AgencyId_ReservationId_SentAt",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ReservationId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "ChatMessages");

            migrationBuilder.AlterColumn<int>(
                name: "RentingId",
                table: "ChatMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
