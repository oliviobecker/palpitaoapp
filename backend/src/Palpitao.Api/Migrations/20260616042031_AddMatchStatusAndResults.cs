using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchStatusAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsUpdatedAt",
                table: "Rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalMatchId",
                table: "RoundMatches",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalMatchUrl",
                table: "RoundMatches",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResultUpdatedAt",
                table: "RoundMatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultSource",
                table: "RoundMatches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RoundMatches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotStarted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultsUpdatedAt",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "ExternalMatchId",
                table: "RoundMatches");

            migrationBuilder.DropColumn(
                name: "ExternalMatchUrl",
                table: "RoundMatches");

            migrationBuilder.DropColumn(
                name: "LastResultUpdatedAt",
                table: "RoundMatches");

            migrationBuilder.DropColumn(
                name: "ResultSource",
                table: "RoundMatches");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RoundMatches");
        }
    }
}
