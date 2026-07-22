using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemSolution.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new nullable FK columns alongside the existing URL
            //    string columns so both exist during the backfill.
            migrationBuilder.AddColumn<int>(
                name: "FactureFileId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CINFileId",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DrivingLicenceFileId",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasseportFileId",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhotoFileId",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgencyId = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoredFiles_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_FactureFileId",
                table: "Expenses",
                column: "FactureFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CINFileId",
                table: "Clients",
                column: "CINFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_DrivingLicenceFileId",
                table: "Clients",
                column: "DrivingLicenceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PasseportFileId",
                table: "Clients",
                column: "PasseportFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_PhotoFileId",
                table: "Cars",
                column: "PhotoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_AgencyId_Sha256",
                table: "StoredFiles",
                columns: new[] { "AgencyId", "Sha256" });

            // 2. Backfill: turn each existing image URL into a StoredFile record
            //    and point the new FK at it. The bytes aren't available at
            //    migration time, so Size/MimeType/Sha256/OriginalFileName are
            //    left empty (degraded legacy metadata) — an empty Sha256 simply
            //    never dedups; Path and Url carry the original URL. New uploads
            //    populate every field. The MERGE .. OUTPUT trick correlates each
            //    inserted StoredFile back to its source row so the FK can be set.
            migrationBuilder.Sql(@"
DECLARE @cinMap TABLE (SourceId int, FileId int);
MERGE INTO [StoredFiles] AS t
USING (SELECT [Id], [AgencyId], [CINImageUrl] FROM [Clients]
       WHERE [CINImageUrl] IS NOT NULL AND LTRIM(RTRIM([CINImageUrl])) <> '') AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT ([AgencyId], [Path], [Url], [OriginalFileName], [MimeType], [Size], [Sha256], [DocumentType], [CreatedOn])
    VALUES (s.[AgencyId], s.[CINImageUrl], s.[CINImageUrl], '', '', 0, '', 0, SYSDATETIMEOFFSET())
OUTPUT s.[Id], inserted.[Id] INTO @cinMap (SourceId, FileId);
UPDATE c SET [CINFileId] = m.FileId FROM [Clients] c INNER JOIN @cinMap m ON c.[Id] = m.SourceId;");

            migrationBuilder.Sql(@"
DECLARE @dlMap TABLE (SourceId int, FileId int);
MERGE INTO [StoredFiles] AS t
USING (SELECT [Id], [AgencyId], [DrivingLicenceImageUrl] FROM [Clients]
       WHERE [DrivingLicenceImageUrl] IS NOT NULL AND LTRIM(RTRIM([DrivingLicenceImageUrl])) <> '') AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT ([AgencyId], [Path], [Url], [OriginalFileName], [MimeType], [Size], [Sha256], [DocumentType], [CreatedOn])
    VALUES (s.[AgencyId], s.[DrivingLicenceImageUrl], s.[DrivingLicenceImageUrl], '', '', 0, '', 1, SYSDATETIMEOFFSET())
OUTPUT s.[Id], inserted.[Id] INTO @dlMap (SourceId, FileId);
UPDATE c SET [DrivingLicenceFileId] = m.FileId FROM [Clients] c INNER JOIN @dlMap m ON c.[Id] = m.SourceId;");

            migrationBuilder.Sql(@"
DECLARE @passeportMap TABLE (SourceId int, FileId int);
MERGE INTO [StoredFiles] AS t
USING (SELECT [Id], [AgencyId], [PasserportImageUrl] FROM [Clients]
       WHERE [PasserportImageUrl] IS NOT NULL AND LTRIM(RTRIM([PasserportImageUrl])) <> '') AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT ([AgencyId], [Path], [Url], [OriginalFileName], [MimeType], [Size], [Sha256], [DocumentType], [CreatedOn])
    VALUES (s.[AgencyId], s.[PasserportImageUrl], s.[PasserportImageUrl], '', '', 0, '', 2, SYSDATETIMEOFFSET())
OUTPUT s.[Id], inserted.[Id] INTO @passeportMap (SourceId, FileId);
UPDATE c SET [PasseportFileId] = m.FileId FROM [Clients] c INNER JOIN @passeportMap m ON c.[Id] = m.SourceId;");

            migrationBuilder.Sql(@"
DECLARE @carMap TABLE (SourceId int, FileId int);
MERGE INTO [StoredFiles] AS t
USING (SELECT [Id], [AgencyId], [ImageUrl] FROM [Cars]
       WHERE [ImageUrl] IS NOT NULL AND LTRIM(RTRIM([ImageUrl])) <> '') AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT ([AgencyId], [Path], [Url], [OriginalFileName], [MimeType], [Size], [Sha256], [DocumentType], [CreatedOn])
    VALUES (s.[AgencyId], s.[ImageUrl], s.[ImageUrl], '', '', 0, '', 3, SYSDATETIMEOFFSET())
OUTPUT s.[Id], inserted.[Id] INTO @carMap (SourceId, FileId);
UPDATE c SET [PhotoFileId] = m.FileId FROM [Cars] c INNER JOIN @carMap m ON c.[Id] = m.SourceId;");

            migrationBuilder.Sql(@"
DECLARE @factureMap TABLE (SourceId int, FileId int);
MERGE INTO [StoredFiles] AS t
USING (SELECT [Id], [AgencyId], [FactureImageUrl] FROM [Expenses]
       WHERE [FactureImageUrl] IS NOT NULL AND LTRIM(RTRIM([FactureImageUrl])) <> '') AS s
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT ([AgencyId], [Path], [Url], [OriginalFileName], [MimeType], [Size], [Sha256], [DocumentType], [CreatedOn])
    VALUES (s.[AgencyId], s.[FactureImageUrl], s.[FactureImageUrl], '', '', 0, '', 4, SYSDATETIMEOFFSET())
OUTPUT s.[Id], inserted.[Id] INTO @factureMap (SourceId, FileId);
UPDATE e SET [FactureFileId] = m.FileId FROM [Expenses] e INNER JOIN @factureMap m ON e.[Id] = m.SourceId;");

            // 3. Add the FKs now that every value is a valid StoredFile id.
            migrationBuilder.AddForeignKey(
                name: "FK_Cars_StoredFiles_PhotoFileId",
                table: "Cars",
                column: "PhotoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_StoredFiles_CINFileId",
                table: "Clients",
                column: "CINFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_StoredFiles_DrivingLicenceFileId",
                table: "Clients",
                column: "DrivingLicenceFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_StoredFiles_PasseportFileId",
                table: "Clients",
                column: "PasseportFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_StoredFiles_FactureFileId",
                table: "Expenses",
                column: "FactureFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. Drop the now-migrated URL string columns.
            migrationBuilder.DropColumn(
                name: "FactureImageUrl",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CINImageUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceImageUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PasserportImageUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Cars");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the URL string columns, then copy each StoredFile's Url back
            // before dropping the FKs and the table.
            migrationBuilder.AddColumn<string>(
                name: "FactureImageUrl",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CINImageUrl",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrivingLicenceImageUrl",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasserportImageUrl",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Cars",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE c SET [CINImageUrl] = f.[Url] FROM [Clients] c INNER JOIN [StoredFiles] f ON c.[CINFileId] = f.[Id];
UPDATE c SET [DrivingLicenceImageUrl] = f.[Url] FROM [Clients] c INNER JOIN [StoredFiles] f ON c.[DrivingLicenceFileId] = f.[Id];
UPDATE c SET [PasserportImageUrl] = f.[Url] FROM [Clients] c INNER JOIN [StoredFiles] f ON c.[PasseportFileId] = f.[Id];
UPDATE c SET [ImageUrl] = f.[Url] FROM [Cars] c INNER JOIN [StoredFiles] f ON c.[PhotoFileId] = f.[Id];
UPDATE e SET [FactureImageUrl] = f.[Url] FROM [Expenses] e INNER JOIN [StoredFiles] f ON e.[FactureFileId] = f.[Id];");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_StoredFiles_PhotoFileId",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_StoredFiles_CINFileId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_StoredFiles_DrivingLicenceFileId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_StoredFiles_PasseportFileId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_StoredFiles_FactureFileId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_FactureFileId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CINFileId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_DrivingLicenceFileId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_PasseportFileId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Cars_PhotoFileId",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "FactureFileId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CINFileId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceFileId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PasseportFileId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PhotoFileId",
                table: "Cars");
        }
    }
}
