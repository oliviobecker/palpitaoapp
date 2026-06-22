using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsBigSevenClub = table.Column<bool>(type: "boolean", nullable: false),
                    CrestUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEliminated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FirstMatchStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MirrorPublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FlavioDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FlavioConflictAlert = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rounds_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rounds_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Standings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PlayedRounds = table.Column<int>(type: "integer", nullable: false),
                    ExactCount = table.Column<int>(type: "integer", nullable: false),
                    AbsenceCount = table.Column<int>(type: "integer", nullable: false),
                    PenaltyPoints = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Standings_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Standings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AbsenceOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAbsent = table.Column<bool>(type: "boolean", nullable: false),
                    Justification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbsenceOverrides_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbsenceOverrides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Absences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceNumber = table.Column<int>(type: "integer", nullable: false),
                    PenaltyPoints = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Absences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Absences_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Absences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OcrImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StoredFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    LanguageUsed = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrImportBatches_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Competition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Phase = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: true),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    ManualMultiplierOverride = table.Column<int>(type: "integer", nullable: true),
                    ManualMultiplierJustification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundMatches_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundMatches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoundMatches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoundParticipantResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossPoints = table.Column<int>(type: "integer", nullable: false),
                    FinalPoints = table.Column<int>(type: "integer", nullable: false),
                    PenaltyPoints = table.Column<int>(type: "integer", nullable: false),
                    WasAbsent = table.Column<bool>(type: "boolean", nullable: false),
                    WasEliminated = table.Column<bool>(type: "boolean", nullable: false),
                    FlavioRuleApplied = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundParticipantResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundParticipantResults_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundParticipantResults_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundParticipantResults_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OcrPredictionCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OcrImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParticipantNameRaw = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RoundMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchTextRaw = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PredictedHomeScore = table.Column<int>(type: "integer", nullable: true),
                    PredictedAwayScore = table.Column<int>(type: "integer", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    NeedsReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrPredictionCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrPredictionCandidates_OcrImportBatches_OcrImportBatchId",
                        column: x => x.OcrImportBatchId,
                        principalTable: "OcrImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictedHomeScore = table.Column<int>(type: "integer", nullable: false),
                    PredictedAwayScore = table.Column<int>(type: "integer", nullable: false),
                    ScoreCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_RoundMatches_RoundMatchId",
                        column: x => x.RoundMatchId,
                        principalTable: "RoundMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Predictions_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Predictions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PredictionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasePoints = table.Column<int>(type: "integer", nullable: false),
                    Multiplier = table.Column<int>(type: "integer", nullable: false),
                    FinalPoints = table.Column<int>(type: "integer", nullable: false),
                    ScoreCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsExactScore = table.Column<bool>(type: "boolean", nullable: false),
                    IsCorrectColumn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionScores_RoundMatches_RoundMatchId",
                        column: x => x.RoundMatchId,
                        principalTable: "RoundMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PredictionScores_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PredictionScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "CreatedAt", "CrestUrl", "IsBigSevenClub", "Name", "ShortName" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Arsenal", "ARS" },
                    { new Guid("11111111-1111-1111-1111-111111111102"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Chelsea", "CHE" },
                    { new Guid("11111111-1111-1111-1111-111111111103"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Liverpool", "LIV" },
                    { new Guid("11111111-1111-1111-1111-111111111104"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Manchester City", "MCI" },
                    { new Guid("11111111-1111-1111-1111-111111111105"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Manchester United", "MUN" },
                    { new Guid("11111111-1111-1111-1111-111111111106"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Newcastle", "NEW" },
                    { new Guid("11111111-1111-1111-1111-111111111107"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Tottenham", "TOT" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsActive", "IsEliminated", "Name", "PasswordHash", "Role" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222201"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@palpitao.local", true, false, "Administrador", "$2a$11$rqqFHI1KeD4V96P8cdiPBeR8U8MEQEwED.AbOQ2aeuQeAeNJN3U.m", "Admin" });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceOverrides_RoundId_UserId",
                table: "AbsenceOverrides",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceOverrides_UserId",
                table: "AbsenceOverrides",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Absences_RoundId_UserId",
                table: "Absences",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Absences_UserId",
                table: "Absences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OcrImportBatches_RoundId",
                table: "OcrImportBatches",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OcrPredictionCandidates_OcrImportBatchId",
                table: "OcrPredictionCandidates",
                column: "OcrImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_RoundId_UserId",
                table: "Predictions",
                columns: new[] { "RoundId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_RoundMatchId_UserId",
                table: "Predictions",
                columns: new[] { "RoundMatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionScores_RoundId_UserId",
                table: "PredictionScores",
                columns: new[] { "RoundId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PredictionScores_RoundMatchId_UserId",
                table: "PredictionScores",
                columns: new[] { "RoundMatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionScores_UserId",
                table: "PredictionScores",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundMatches_AwayTeamId",
                table: "RoundMatches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundMatches_HomeTeamId",
                table: "RoundMatches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundMatches_RoundId",
                table: "RoundMatches",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipantResults_RoundId_UserId",
                table: "RoundParticipantResults",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipantResults_SeasonId_UserId",
                table: "RoundParticipantResults",
                columns: new[] { "SeasonId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipantResults_UserId",
                table: "RoundParticipantResults",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_CreatedByUserId",
                table: "Rounds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_SeasonId_Number",
                table: "Rounds",
                columns: new[] { "SeasonId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Standings_SeasonId_UserId",
                table: "Standings",
                columns: new[] { "SeasonId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Standings_UserId",
                table: "Standings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbsenceOverrides");

            migrationBuilder.DropTable(
                name: "Absences");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "OcrPredictionCandidates");

            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "PredictionScores");

            migrationBuilder.DropTable(
                name: "RoundParticipantResults");

            migrationBuilder.DropTable(
                name: "Standings");

            migrationBuilder.DropTable(
                name: "OcrImportBatches");

            migrationBuilder.DropTable(
                name: "RoundMatches");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
