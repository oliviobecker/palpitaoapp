using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class MovePerGroupFlagsToGroupUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the per-group flags (existing rows default to active/not-eliminated).
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "GroupUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEliminated",
                table: "GroupUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 2) Backfill from the old global User flags BEFORE dropping them, so any
            //    currently deactivated/eliminated participant keeps that state in every
            //    group they belong to (matches the pre-migration global behaviour).
            migrationBuilder.Sql(
                """
                UPDATE "GroupUsers" gu
                SET "IsActive" = u."IsActive",
                    "IsEliminated" = u."IsEliminated"
                FROM "Users" u
                WHERE gu."UserId" = u."Id";
                """);

            // 3) Now the global elimination flag is no longer needed.
            migrationBuilder.DropColumn(
                name: "IsEliminated",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "GroupUsers");

            migrationBuilder.DropColumn(
                name: "IsEliminated",
                table: "GroupUsers");

            migrationBuilder.AddColumn<bool>(
                name: "IsEliminated",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222201"),
                column: "IsEliminated",
                value: false);
        }
    }
}
