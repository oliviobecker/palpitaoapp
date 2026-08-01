using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <summary>
    /// Makes the Flávio Rule threshold and the absence punishments editable per season.
    /// The <c>defaultValue</c> on each column exists to backfill the rows that already
    /// exist with the historical hardcoded numbers (16 / 1 / 20 / 5); it is dropped right
    /// after so the column matches the model, which deliberately declares no store default
    /// (a store default would make EF omit <c>AbsencePenaltyPoints = 0</c> — a legitimate
    /// choice — on insert and silently persist 20 instead).
    /// </summary>
    public partial class SeasonRuleParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbsenceEliminationCount",
                table: "SeasonScoringConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "AbsenceFromRound",
                table: "SeasonScoringConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "AbsencePenaltyPoints",
                table: "SeasonScoringConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "FlavioFromRound",
                table: "SeasonScoringConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 16);

            migrationBuilder.Sql("""
                ALTER TABLE "SeasonScoringConfigs"
                    ALTER COLUMN "AbsenceEliminationCount" DROP DEFAULT,
                    ALTER COLUMN "AbsenceFromRound"        DROP DEFAULT,
                    ALTER COLUMN "AbsencePenaltyPoints"    DROP DEFAULT,
                    ALTER COLUMN "FlavioFromRound"         DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsenceEliminationCount",
                table: "SeasonScoringConfigs");

            migrationBuilder.DropColumn(
                name: "AbsenceFromRound",
                table: "SeasonScoringConfigs");

            migrationBuilder.DropColumn(
                name: "AbsencePenaltyPoints",
                table: "SeasonScoringConfigs");

            migrationBuilder.DropColumn(
                name: "FlavioFromRound",
                table: "SeasonScoringConfigs");
        }
    }
}
