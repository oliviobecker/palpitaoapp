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
                keyValue: new Guid("464c232d-1caa-774c-1477-6ca9c2991738"),
                column: "Division",
                value: 2);

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

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "CountryCode", "CreatedAt", "CrestUrl", "Division", "FifaCode", "IsBigSevenClub", "Name", "ShortName", "TeamType", "WorldCupTitles" },
                values: new object[,]
                {
                    { new Guid("00a969d4-be71-0522-63dc-f3417623cab5"), null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, null, false, "Notts County", "NOT", "Club", 0 },
                    { new Guid("014f92c4-08cc-edcd-5774-4f6a3a82e8f6"), null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, null, false, "Cambridge United", "CAM", "Club", 0 },
                    { new Guid("99d6f86c-a49c-86e1-977c-055eecaf05fd"), null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, null, false, "Bromley", "BRO", "Club", 0 },
                    { new Guid("f6d20fee-a413-b482-f1d1-dcdd579d7c71"), null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, null, false, "Milton Keynes Dons", "MKD", "Club", 0 }
                });
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
                keyValue: new Guid("464c232d-1caa-774c-1477-6ca9c2991738"),
                column: "Division",
                value: 0);

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
