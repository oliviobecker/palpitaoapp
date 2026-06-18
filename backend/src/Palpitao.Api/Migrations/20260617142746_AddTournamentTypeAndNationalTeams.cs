using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTypeAndNationalTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Teams",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FifaCode",
                table: "Teams",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamType",
                table: "Teams",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Club");

            migrationBuilder.AddColumn<int>(
                name: "WorldCupTitles",
                table: "Teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0643d237-1236-a614-6deb-5c1d50489d65"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0b6ec555-86c4-3619-684d-f6e985eb6dab"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0c0d8b61-57be-7e2e-0f32-160c4fb6c6e8"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111105"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111106"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111107"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("157207da-1d38-a621-7402-9bd9c6e0e1ba"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("18337fcd-e014-bbc2-40ba-9e1d5b7d7e4a"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("197657bf-8de2-f68a-f5c4-e69ed9919c03"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("1a6265b5-9b03-67cf-914d-3d09f651c999"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("245363b3-ca70-a57a-95d5-570b2ecd5f6a"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("309f8718-aefc-5b6d-87c4-873e92e05832"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("3324f7f9-7477-4814-9292-4f5498afc2ca"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("332721ec-239e-3a52-743b-f9a10e4ffc74"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("372d2f0b-0d74-5d16-c6e8-4d0c88ee5d30"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("38147db7-f93e-19b4-0a99-b6fc1e79a285"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("3b2c5fa1-a85c-649c-3fc0-e190fd0a663b"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("3e90654b-a2fb-5504-8f97-41fd1fe4f2f1"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("412d6c85-7d1f-e91b-db73-224aa5c6feff"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("412df6cd-a237-a841-56e8-d5aa14d69283"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("452dce33-d510-05e9-9d4c-009b3b524689"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("464c232d-1caa-774c-1477-6ca9c2991738"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("527eb8ae-87e3-4dcd-edcf-1fdc272d065d"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("52aa8de5-bb71-7819-e9e3-9e721ae9fbb1"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("571d2691-c2e5-ad91-0441-e921d97e1e51"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("5d33ab69-d162-b60b-2edc-dc3dbd731177"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("619881df-c28b-a91f-6bb6-9fbc9533545c"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("633f620c-6d3a-18d1-e042-049ce9648fcc"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("639ccdd4-40ed-f52d-f80e-7aa0f0d399dd"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("689e63d3-59cc-e1c3-9060-65e1663b8501"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("71c8f089-3ede-ab7b-7f4c-ba436a85ee96"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("76c73df3-4551-4881-f3f6-a40dc4f0ab36"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("79e62f46-f73d-82a8-a7ee-d974bd26b2e6"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("7a93656f-2b11-497d-51e3-92762e8cca96"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("8608c4c3-d9ea-d5af-d80b-7a64f7e7084a"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("87e33b81-1032-b701-e270-fbf4a0a5e4fa"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("8bacfc2c-633e-4eff-bbaa-d233916b8763"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("94ce7ba0-e73b-79ec-619d-c0817e296bb6"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("962ce353-613c-74db-83d5-cca35bede489"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("9cd2ed8d-098a-b2cb-dcde-d100b88dd3dd"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("9d17cb18-c8d3-c5c7-fe4c-3990d0dc1d6c"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4787bdf-b277-7fde-f8f1-1d2ce6babf6a"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4a9a1a8-a7ee-047e-1753-d0ac5a1288fd"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a606681b-46b1-9faf-919d-a93a8b2e6fd3"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a87325bb-8d0c-d832-fbf2-f6eb99c6d78f"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b1601569-da6c-a45a-f72d-0a0a801132cc"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b42c38ba-d9f9-6898-5c95-0572b8cc4461"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b4b1c616-ac3a-da52-8f53-3925ce003bf0"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b6083d70-d7bb-fc50-453f-e77883d22d15"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b9627c9c-b4dc-2906-cfff-9a1b67fe7872"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("be4ff455-5e31-dc6c-4cf7-e87c01d4062b"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c4cb6ae6-7dc8-efcd-ceb5-2b68e6919110"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c77966c8-92ed-c0fa-396e-475f5da6bd88"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c96f7062-79e3-928c-8ec2-f70182a33e27"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("ce8d4549-4834-494e-3d49-daf0e180e35e"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e226a779-586d-75f6-8704-4b06e8053001"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e81bf932-8829-70a6-e530-fdf18e26adf9"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e8a096f2-7f6b-7a4f-8d39-69489afb69d9"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("eabbc77e-85be-2bb6-40c2-c4f647ac984f"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("ef9f35f5-5195-1954-e356-14d89b4dfad9"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f29f0509-a891-a240-ed5e-b635b29130bd"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f616d444-c35d-1e44-65c7-ce9ae64f734a"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f77c1d3f-d3c5-118f-1a0a-bab078be9695"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f94609eb-94f8-c0ef-ca27-9935d5ce875f"),
                columns: new[] { "CountryCode", "FifaCode", "TeamType", "WorldCupTitles" },
                values: new object[] { null, null, "Club", 0 });

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "CountryCode", "CreatedAt", "CrestUrl", "Division", "FifaCode", "IsBigSevenClub", "Name", "ShortName", "TeamType", "WorldCupTitles" },
                values: new object[,]
                {
                    { new Guid("52301cce-5863-636d-318d-b27ebfa25024"), "DE", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "GER", false, "Germany", "GER", "NationalTeam", 4 },
                    { new Guid("71f27b42-ab7c-4737-09e8-bb01652183ce"), "BR", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "BRA", false, "Brazil", "BRA", "NationalTeam", 5 },
                    { new Guid("9545115a-c6f2-0dd4-d348-382368a54f06"), "AR", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "ARG", false, "Argentina", "ARG", "NationalTeam", 3 },
                    { new Guid("ae6e37b7-826b-a375-35d3-d0b34195180b"), "FR", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "FRA", false, "France", "FRA", "NationalTeam", 2 },
                    { new Guid("b45e5449-8c83-673e-e468-68bb04f633f2"), "GB", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "ENG", false, "England", "ENG", "NationalTeam", 1 },
                    { new Guid("d2d20443-0dc4-f5d3-e018-519cc8e64172"), "ES", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "ESP", false, "Spain", "ESP", "NationalTeam", 1 },
                    { new Guid("d5ff7176-17c0-25aa-7376-711eacfd3658"), "UY", new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "URU", false, "Uruguay", "URU", "NationalTeam", 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("52301cce-5863-636d-318d-b27ebfa25024"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("71f27b42-ab7c-4737-09e8-bb01652183ce"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("9545115a-c6f2-0dd4-d348-382368a54f06"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("ae6e37b7-826b-a375-35d3-d0b34195180b"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b45e5449-8c83-673e-e468-68bb04f633f2"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("d2d20443-0dc4-f5d3-e018-519cc8e64172"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("d5ff7176-17c0-25aa-7376-711eacfd3658"));

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "FifaCode",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TeamType",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "WorldCupTitles",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TournamentType",
                table: "Groups");
        }
    }
}
