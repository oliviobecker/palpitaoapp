using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeasonScoringConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonScoringConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("33333333-3333-3333-3333-333333333301")),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnOnlyPoints = table.Column<int>(type: "integer", nullable: false),
                    TraditionalPoints = table.Column<int>(type: "integer", nullable: false),
                    MediumPoints = table.Column<int>(type: "integer", nullable: false),
                    UncommonPoints = table.Column<int>(type: "integer", nullable: false),
                    ExtraUncommonPoints = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonScoringConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonScoringConfigs_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonScoringConfigs_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoringClassicTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringClassicTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringClassicTeams_SeasonScoringConfigs_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "SeasonScoringConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoringClassicTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoringMultiplierRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Competition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Phase = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Multiplier = table.Column<int>(type: "integer", nullable: false),
                    ClassicMultiplier = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringMultiplierRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringMultiplierRules_SeasonScoringConfigs_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "SeasonScoringConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoringScoreEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Low = table.Column<int>(type: "integer", nullable: false),
                    High = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringScoreEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringScoreEntries_SeasonScoringConfigs_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "SeasonScoringConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoringClassicTeams_ConfigId_TeamId",
                table: "ScoringClassicTeams",
                columns: new[] { "ConfigId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringClassicTeams_TeamId",
                table: "ScoringClassicTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringMultiplierRules_ConfigId_Competition_Phase",
                table: "ScoringMultiplierRules",
                columns: new[] { "ConfigId", "Competition", "Phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringScoreEntries_ConfigId_Low_High",
                table: "ScoringScoreEntries",
                columns: new[] { "ConfigId", "Low", "High" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonScoringConfigs_GroupId",
                table: "SeasonScoringConfigs",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonScoringConfigs_SeasonId",
                table: "SeasonScoringConfigs",
                column: "SeasonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoringClassicTeams");

            migrationBuilder.DropTable(
                name: "ScoringMultiplierRules");

            migrationBuilder.DropTable(
                name: "ScoringScoreEntries");

            migrationBuilder.DropTable(
                name: "SeasonScoringConfigs");
        }
    }
}
