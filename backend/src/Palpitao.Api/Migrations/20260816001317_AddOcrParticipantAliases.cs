using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrParticipantAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OcrParticipantAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("33333333-3333-3333-3333-333333333301")),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AliasRaw = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrParticipantAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrParticipantAliases_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OcrParticipantAliases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcrParticipantAliases_GroupId",
                table: "OcrParticipantAliases",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_OcrParticipantAliases_GroupId_Alias",
                table: "OcrParticipantAliases",
                columns: new[] { "GroupId", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcrParticipantAliases_UserId",
                table: "OcrParticipantAliases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcrParticipantAliases");
        }
    }
}
