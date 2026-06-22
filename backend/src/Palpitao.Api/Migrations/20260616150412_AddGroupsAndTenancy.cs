using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupsAndTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Standings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("33333333-3333-3333-3333-333333333301"));

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Seasons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("33333333-3333-3333-3333-333333333301"));

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Rounds",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("33333333-3333-3333-3333-333333333301"));

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "RoundParticipantResults",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("33333333-3333-3333-3333-333333333301"));

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupUsers_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[] { "Id", "CreatedAt", "CreatedByUserId", "Description", "IsActive", "Name", "OwnerUserId", "Slug", "UpdatedAt" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333301"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("22222222-2222-2222-2222-222222222201"), "Bolão da temporada inglesa.", true, "Palpitão England 2025/2026", new Guid("22222222-2222-2222-2222-222222222201"), "palpitao-england-2025-2026", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "GroupUsers",
                columns: new[] { "Id", "ApprovedAt", "ApprovedByUserId", "CreatedAt", "GroupId", "RejectedAt", "RejectedByUserId", "RejectionReason", "Role", "Status", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333302"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("33333333-3333-3333-3333-333333333301"), null, null, null, "GroupAdmin", "Approved", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("22222222-2222-2222-2222-222222222201") });

            migrationBuilder.CreateIndex(
                name: "IX_Standings_GroupId",
                table: "Standings",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_GroupId",
                table: "Seasons",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_GroupId",
                table: "Rounds",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipantResults_GroupId",
                table: "RoundParticipantResults",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_GroupId",
                table: "AuditLogs",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_IsActive",
                table: "Groups",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Slug",
                table: "Groups",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupUsers_GroupId_Status",
                table: "GroupUsers",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupUsers_GroupId_UserId",
                table: "GroupUsers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupUsers_UserId",
                table: "GroupUsers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoundParticipantResults_Groups_GroupId",
                table: "RoundParticipantResults",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rounds_Groups_GroupId",
                table: "Rounds",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Groups_GroupId",
                table: "Seasons",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Standings_Groups_GroupId",
                table: "Standings",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundParticipantResults_Groups_GroupId",
                table: "RoundParticipantResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Rounds_Groups_GroupId",
                table: "Rounds");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Groups_GroupId",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Standings_Groups_GroupId",
                table: "Standings");

            migrationBuilder.DropTable(
                name: "GroupUsers");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Standings_GroupId",
                table: "Standings");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_GroupId",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Rounds_GroupId",
                table: "Rounds");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipantResults_GroupId",
                table: "RoundParticipantResults");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_GroupId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Standings");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "RoundParticipantResults");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "AuditLogs");
        }
    }
}
