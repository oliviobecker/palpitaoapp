using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrImportImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dead since InitialCreate: declared and mapped, but never written or read by any
            // code, so every row holds NULL. Uploads are stored in OcrImportImages below, not on
            // disk. Down() recreates the column.
            migrationBuilder.DropColumn(
                name: "StoredFilePath",
                table: "OcrImportBatches");

            migrationBuilder.CreateTable(
                name: "OcrImportImages",
                columns: table => new
                {
                    OcrImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ByteSize = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrImportImages", x => x.OcrImportBatchId);
                    table.ForeignKey(
                        name: "FK_OcrImportImages_OcrImportBatches_OcrImportBatchId",
                        column: x => x.OcrImportBatchId,
                        principalTable: "OcrImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcrImportImages_CreatedAt",
                table: "OcrImportImages",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcrImportImages");

            migrationBuilder.AddColumn<string>(
                name: "StoredFilePath",
                table: "OcrImportBatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
