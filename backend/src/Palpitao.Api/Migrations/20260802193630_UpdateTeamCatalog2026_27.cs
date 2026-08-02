using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTeamCatalog2026_27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0c0d8b61-57be-7e2e-0f32-160c4fb6c6e8"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("197657bf-8de2-f68a-f5c4-e69ed9919c03"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("1a6265b5-9b03-67cf-914d-3d09f651c999"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("309f8718-aefc-5b6d-87c4-873e92e05832"),
                column: "Division",
                value: null);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("452dce33-d510-05e9-9d4c-009b3b524689"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("527eb8ae-87e3-4dcd-edcf-1fdc272d065d"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("79e62f46-f73d-82a8-a7ee-d974bd26b2e6"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("94ce7ba0-e73b-79ec-619d-c0817e296bb6"),
                column: "Division",
                value: null);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4787bdf-b277-7fde-f8f1-1d2ce6babf6a"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4a9a1a8-a7ee-047e-1753-d0ac5a1288fd"),
                column: "Division",
                value: null);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a87325bb-8d0c-d832-fbf2-f6eb99c6d78f"),
                column: "Division",
                value: null);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c77966c8-92ed-c0fa-396e-475f5da6bd88"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("d2d20443-0dc4-f5d3-e018-519cc8e64172"),
                column: "WorldCupTitles",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e81bf932-8829-70a6-e530-fdf18e26adf9"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e8a096f2-7f6b-7a4f-8d39-69489afb69d9"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f29f0509-a891-a240-ed5e-b635b29130bd"),
                column: "Division",
                value: 3);

            // Four clubs join the catalogue, and they may already be in the database:
            // FixtureImportService.ResolveTeam creates a missing team on the fly with a
            // *random* Id, and an FA Cup fixture can pull in a club from outside the
            // three tracked divisions. A plain InsertData then trips IX_Teams_Name and
            // takes the whole deploy down with it — which is what happened on production.
            //
            // So: insert only the clubs that are genuinely absent, matching on the name
            // (the constraint that failed) as well as the Id (so a re-run is a no-op).
            //
            // A club that is already there keeps its import-created Id, which means it
            // no longer matches the name-derived Id in HasData — later UpdateData calls
            // keyed on that Id will miss it. Reconciling that means repointing the
            // RoundMatches/ScoringClassicTeams foreign keys, which needs a look at the
            // real production rows first; it is deliberately not done blind here.
            migrationBuilder.Sql("""
                INSERT INTO "Teams"
                    ("Id", "CountryCode", "CreatedAt", "CrestUrl", "Division", "FifaCode",
                     "IsBigSevenClub", "Name", "ShortName", "TeamType", "WorldCupTitles")
                SELECT
                    v.id, NULL, TIMESTAMPTZ '2025-07-01 00:00:00+00', NULL, 3, NULL,
                    false, v.name, v.short_name, 'Club', 0
                FROM (VALUES
                    ('00a969d4-be71-0522-63dc-f3417623cab5'::uuid, 'Notts County', 'NOT'),
                    ('014f92c4-08cc-edcd-5774-4f6a3a82e8f6'::uuid, 'Cambridge United', 'CAM'),
                    ('99d6f86c-a49c-86e1-977c-055eecaf05fd'::uuid, 'Bromley', 'BRO'),
                    ('f6d20fee-a413-b482-f1d1-dcdd579d7c71'::uuid, 'Milton Keynes Dons', 'MKD')
                ) AS v(id, name, short_name)
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Teams" t WHERE t."Name" = v.name OR t."Id" = v.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("00a969d4-be71-0522-63dc-f3417623cab5"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("014f92c4-08cc-edcd-5774-4f6a3a82e8f6"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("99d6f86c-a49c-86e1-977c-055eecaf05fd"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f6d20fee-a413-b482-f1d1-dcdd579d7c71"));

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0c0d8b61-57be-7e2e-0f32-160c4fb6c6e8"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("197657bf-8de2-f68a-f5c4-e69ed9919c03"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("1a6265b5-9b03-67cf-914d-3d09f651c999"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("309f8718-aefc-5b6d-87c4-873e92e05832"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("452dce33-d510-05e9-9d4c-009b3b524689"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("527eb8ae-87e3-4dcd-edcf-1fdc272d065d"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("79e62f46-f73d-82a8-a7ee-d974bd26b2e6"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("94ce7ba0-e73b-79ec-619d-c0817e296bb6"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4787bdf-b277-7fde-f8f1-1d2ce6babf6a"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4a9a1a8-a7ee-047e-1753-d0ac5a1288fd"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a87325bb-8d0c-d832-fbf2-f6eb99c6d78f"),
                column: "Division",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c77966c8-92ed-c0fa-396e-475f5da6bd88"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("d2d20443-0dc4-f5d3-e018-519cc8e64172"),
                column: "WorldCupTitles",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e81bf932-8829-70a6-e530-fdf18e26adf9"),
                column: "Division",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e8a096f2-7f6b-7a4f-8d39-69489afb69d9"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f29f0509-a891-a240-ed5e-b635b29130bd"),
                column: "Division",
                value: 2);
        }
    }
}
