using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <summary>
    /// Gives every classic team a group, so a classic is a pair from the <em>same</em> group
    /// rather than any two teams on a flat list. Existing rows are Big Seven clubs (England) or
    /// world champions (World Cup): the column default covers the first case and the SQL below
    /// fixes up the second. Also moves West Ham United to the Championship in the club
    /// catalogue, where it now forms the default classic pair with Millwall.
    /// </summary>
    public partial class AddScoringClassicTeamCompetition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default exists only to backfill the existing England rows and is dropped
            // right after, so the column matches the model, which declares no store default.
            migrationBuilder.AddColumn<string>(
                name: "Competition",
                table: "ScoringClassicTeams",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "PremierLeague");

            // World Cup seasons: their classics are the world champions, not Premier League clubs.
            migrationBuilder.Sql("""
                UPDATE "ScoringClassicTeams" AS ct
                SET "Competition" = 'FifaWorldCup'
                FROM "SeasonScoringConfigs" AS c
                INNER JOIN "Seasons" AS s ON s."Id" = c."SeasonId"
                WHERE ct."ConfigId" = c."Id" AND s."TournamentType" = 'FifaWorldCup';

                ALTER TABLE "ScoringClassicTeams" ALTER COLUMN "Competition" DROP DEFAULT;
                """);

            // Division is stored as an int: 2 = Championship, 0 = PremierLeague.
            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("464c232d-1caa-774c-1477-6ca9c2991738"),
                column: "Division",
                value: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Competition",
                table: "ScoringClassicTeams");

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("464c232d-1caa-774c-1477-6ca9c2991738"),
                column: "Division",
                value: 0);
        }
    }
}
