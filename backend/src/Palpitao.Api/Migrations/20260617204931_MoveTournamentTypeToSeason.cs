using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class MoveTournamentTypeToSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the new column on Seasons (defaulting existing rows to England).
            migrationBuilder.AddColumn<string>(
                name: "TournamentType",
                table: "Seasons",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "PalpitaoEngland");

            // 2) Backfill: each season inherits its owning group's certame type BEFORE
            //    the Groups column is dropped, so no existing data is lost.
            migrationBuilder.Sql(
                "UPDATE \"Seasons\" SET \"TournamentType\" = g.\"TournamentType\" " +
                "FROM \"Groups\" g WHERE \"Seasons\".\"GroupId\" = g.\"Id\";");

            // 3) Drop the now-redundant column from Groups.
            migrationBuilder.DropColumn(
                name: "TournamentType",
                table: "Groups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TournamentType",
                table: "Seasons");

            migrationBuilder.AddColumn<string>(
                name: "TournamentType",
                table: "Groups",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "PalpitaoEngland");

            migrationBuilder.UpdateData(
                table: "Groups",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"),
                column: "TournamentType",
                value: "PalpitaoEngland");
        }
    }
}
