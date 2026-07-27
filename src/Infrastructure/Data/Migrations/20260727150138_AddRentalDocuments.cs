using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    RentingId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentFileId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Rentings_RentingId",
                        column: x => x.RentingId,
                        principalTable: "Rentings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_StoredFiles_DocumentFileId",
                        column: x => x.DocumentFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Factures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    RentingId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RentalAmountCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    ExtraServicesAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExtraServicesAmountCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalAmountCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    DocumentFileId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factures_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factures_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factures_Rentings_RentingId",
                        column: x => x.RentingId,
                        principalTable: "Rentings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factures_StoredFiles_DocumentFileId",
                        column: x => x.DocumentFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_AgencyId_RentingId",
                table: "Contracts",
                columns: new[] { "AgencyId", "RentingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_AgencyId_Year_SequenceNumber",
                table: "Contracts",
                columns: new[] { "AgencyId", "Year", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_DocumentFileId",
                table: "Contracts",
                column: "DocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_RentingId",
                table: "Contracts",
                column: "RentingId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_AgencyId_RentingId",
                table: "Factures",
                columns: new[] { "AgencyId", "RentingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Factures_AgencyId_Year_SequenceNumber",
                table: "Factures",
                columns: new[] { "AgencyId", "Year", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factures_ClientId",
                table: "Factures",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_DocumentFileId",
                table: "Factures",
                column: "DocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_RentingId",
                table: "Factures",
                column: "RentingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Factures");
        }
    }
}
