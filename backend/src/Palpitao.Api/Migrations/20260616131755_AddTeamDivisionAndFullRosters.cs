using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamDivisionAndFullRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Division",
                table: "Teams",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111105"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111106"),
                column: "Division",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111107"),
                column: "Division",
                value: 0);

            migrationBuilder.InsertData(
                table: "Teams",
                columns: new[] { "Id", "CreatedAt", "CrestUrl", "Division", "IsBigSevenClub", "Name", "ShortName" },
                values: new object[,]
                {
                    { new Guid("0643d237-1236-a614-6deb-5c1d50489d65"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Stoke City", "STK" },
                    { new Guid("0b6ec555-86c4-3619-684d-f6e985eb6dab"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Blackpool", "BLA" },
                    { new Guid("0c0d8b61-57be-7e2e-0f32-160c4fb6c6e8"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Lincoln City", "LIN" },
                    { new Guid("157207da-1d38-a621-7402-9bd9c6e0e1ba"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Leyton Orient", "LEY" },
                    { new Guid("18337fcd-e014-bbc2-40ba-9e1d5b7d7e4a"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Middlesbrough", "MID" },
                    { new Guid("197657bf-8de2-f68a-f5c4-e69ed9919c03"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Ipswich Town", "IPS" },
                    { new Guid("1a6265b5-9b03-67cf-914d-3d09f651c999"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Wolverhampton Wanderers", "WOL" },
                    { new Guid("245363b3-ca70-a57a-95d5-570b2ecd5f6a"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Aston Villa", "AVL" },
                    { new Guid("309f8718-aefc-5b6d-87c4-873e92e05832"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Exeter City", "EXE" },
                    { new Guid("3324f7f9-7477-4814-9292-4f5498afc2ca"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Mansfield Town", "MNF" },
                    { new Guid("332721ec-239e-3a52-743b-f9a10e4ffc74"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Crystal Palace", "CRY" },
                    { new Guid("372d2f0b-0d74-5d16-c6e8-4d0c88ee5d30"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Sheffield United", "SHU" },
                    { new Guid("38147db7-f93e-19b4-0a99-b6fc1e79a285"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Swansea City", "SWA" },
                    { new Guid("3b2c5fa1-a85c-649c-3fc0-e190fd0a663b"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Charlton Athletic", "CHA" },
                    { new Guid("3e90654b-a2fb-5504-8f97-41fd1fe4f2f1"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Portsmouth", "POR" },
                    { new Guid("412d6c85-7d1f-e91b-db73-224aa5c6feff"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Preston North End", "PNE" },
                    { new Guid("412df6cd-a237-a841-56e8-d5aa14d69283"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Stockport County", "STO" },
                    { new Guid("452dce33-d510-05e9-9d4c-009b3b524689"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Sheffield Wednesday", "SHW" },
                    { new Guid("464c232d-1caa-774c-1477-6ca9c2991738"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "West Ham United", "WHU" },
                    { new Guid("527eb8ae-87e3-4dcd-edcf-1fdc272d065d"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Bolton Wanderers", "BOL" },
                    { new Guid("52aa8de5-bb71-7819-e9e3-9e721ae9fbb1"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "AFC Wimbledon", "WIM" },
                    { new Guid("571d2691-c2e5-ad91-0441-e921d97e1e51"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Blackburn Rovers", "BLB" },
                    { new Guid("5d33ab69-d162-b60b-2edc-dc3dbd731177"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Barnsley", "BAR" },
                    { new Guid("619881df-c28b-a91f-6bb6-9fbc9533545c"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Wigan Athletic", "WIG" },
                    { new Guid("633f620c-6d3a-18d1-e042-049ce9648fcc"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Wrexham", "WRE" },
                    { new Guid("639ccdd4-40ed-f52d-f80e-7aa0f0d399dd"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Nottingham Forest", "NFO" },
                    { new Guid("689e63d3-59cc-e1c3-9060-65e1663b8501"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Queens Park Rangers", "QPR" },
                    { new Guid("71c8f089-3ede-ab7b-7f4c-ba436a85ee96"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Brentford", "BRE" },
                    { new Guid("76c73df3-4551-4881-f3f6-a40dc4f0ab36"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Plymouth Argyle", "PLY" },
                    { new Guid("79e62f46-f73d-82a8-a7ee-d974bd26b2e6"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Oxford United", "OXF" },
                    { new Guid("7a93656f-2b11-497d-51e3-92762e8cca96"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Wycombe Wanderers", "WYC" },
                    { new Guid("8608c4c3-d9ea-d5af-d80b-7a64f7e7084a"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Fulham", "FUL" },
                    { new Guid("87e33b81-1032-b701-e270-fbf4a0a5e4fa"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Brighton & Hove Albion", "BHA" },
                    { new Guid("8bacfc2c-633e-4eff-bbaa-d233916b8763"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Burton Albion", "BTN" },
                    { new Guid("94ce7ba0-e73b-79ec-619d-c0817e296bb6"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Port Vale", "PVL" },
                    { new Guid("962ce353-613c-74db-83d5-cca35bede489"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Bristol City", "BRC" },
                    { new Guid("9cd2ed8d-098a-b2cb-dcde-d100b88dd3dd"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Millwall", "MIL" },
                    { new Guid("9d17cb18-c8d3-c5c7-fe4c-3990d0dc1d6c"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Southampton", "SOU" },
                    { new Guid("a4787bdf-b277-7fde-f8f1-1d2ce6babf6a"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Cardiff City", "CAR" },
                    { new Guid("a4a9a1a8-a7ee-047e-1753-d0ac5a1288fd"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Northampton Town", "NTH" },
                    { new Guid("a606681b-46b1-9faf-919d-a93a8b2e6fd3"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Bradford City", "BRA" },
                    { new Guid("a87325bb-8d0c-d832-fbf2-f6eb99c6d78f"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Rotherham United", "ROT" },
                    { new Guid("b1601569-da6c-a45a-f72d-0a0a801132cc"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Doncaster Rovers", "DON" },
                    { new Guid("b42c38ba-d9f9-6898-5c95-0572b8cc4461"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Huddersfield Town", "HUD" },
                    { new Guid("b4b1c616-ac3a-da52-8f53-3925ce003bf0"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Reading", "REA" },
                    { new Guid("b6083d70-d7bb-fc50-453f-e77883d22d15"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Luton Town", "LUT" },
                    { new Guid("b9627c9c-b4dc-2906-cfff-9a1b67fe7872"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Sunderland", "SUN" },
                    { new Guid("be4ff455-5e31-dc6c-4cf7-e87c01d4062b"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Norwich City", "NOR" },
                    { new Guid("c4cb6ae6-7dc8-efcd-ceb5-2b68e6919110"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Derby County", "DER" },
                    { new Guid("c77966c8-92ed-c0fa-396e-475f5da6bd88"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Hull City", "HUL" },
                    { new Guid("c96f7062-79e3-928c-8ec2-f70182a33e27"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Bournemouth", "BOU" },
                    { new Guid("ce8d4549-4834-494e-3d49-daf0e180e35e"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Peterborough United", "PET" },
                    { new Guid("e226a779-586d-75f6-8704-4b06e8053001"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, false, "Stevenage", "STV" },
                    { new Guid("e81bf932-8829-70a6-e530-fdf18e26adf9"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Coventry City", "COV" },
                    { new Guid("e8a096f2-7f6b-7a4f-8d39-69489afb69d9"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Burnley", "BUR" },
                    { new Guid("eabbc77e-85be-2bb6-40c2-c4f647ac984f"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "West Bromwich Albion", "WBA" },
                    { new Guid("ef9f35f5-5195-1954-e356-14d89b4dfad9"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Everton", "EVE" },
                    { new Guid("f29f0509-a891-a240-ed5e-b635b29130bd"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Leicester City", "LEI" },
                    { new Guid("f616d444-c35d-1e44-65c7-ce9ae64f734a"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0, false, "Leeds United", "LEE" },
                    { new Guid("f77c1d3f-d3c5-118f-1a0a-bab078be9695"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Birmingham City", "BIR" },
                    { new Guid("f94609eb-94f8-c0ef-ca27-9935d5ce875f"), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, false, "Watford", "WAT" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0643d237-1236-a614-6deb-5c1d50489d65"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0b6ec555-86c4-3619-684d-f6e985eb6dab"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("0c0d8b61-57be-7e2e-0f32-160c4fb6c6e8"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("157207da-1d38-a621-7402-9bd9c6e0e1ba"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("18337fcd-e014-bbc2-40ba-9e1d5b7d7e4a"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("197657bf-8de2-f68a-f5c4-e69ed9919c03"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("1a6265b5-9b03-67cf-914d-3d09f651c999"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("245363b3-ca70-a57a-95d5-570b2ecd5f6a"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("309f8718-aefc-5b6d-87c4-873e92e05832"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("3324f7f9-7477-4814-9292-4f5498afc2ca"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("332721ec-239e-3a52-743b-f9a10e4ffc74"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("372d2f0b-0d74-5d16-c6e8-4d0c88ee5d30"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("38147db7-f93e-19b4-0a99-b6fc1e79a285"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("3b2c5fa1-a85c-649c-3fc0-e190fd0a663b"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("3e90654b-a2fb-5504-8f97-41fd1fe4f2f1"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("412d6c85-7d1f-e91b-db73-224aa5c6feff"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("412df6cd-a237-a841-56e8-d5aa14d69283"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("452dce33-d510-05e9-9d4c-009b3b524689"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("464c232d-1caa-774c-1477-6ca9c2991738"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("527eb8ae-87e3-4dcd-edcf-1fdc272d065d"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("52aa8de5-bb71-7819-e9e3-9e721ae9fbb1"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("571d2691-c2e5-ad91-0441-e921d97e1e51"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("5d33ab69-d162-b60b-2edc-dc3dbd731177"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("619881df-c28b-a91f-6bb6-9fbc9533545c"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("633f620c-6d3a-18d1-e042-049ce9648fcc"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("639ccdd4-40ed-f52d-f80e-7aa0f0d399dd"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("689e63d3-59cc-e1c3-9060-65e1663b8501"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("71c8f089-3ede-ab7b-7f4c-ba436a85ee96"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("76c73df3-4551-4881-f3f6-a40dc4f0ab36"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("79e62f46-f73d-82a8-a7ee-d974bd26b2e6"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("7a93656f-2b11-497d-51e3-92762e8cca96"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("8608c4c3-d9ea-d5af-d80b-7a64f7e7084a"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("87e33b81-1032-b701-e270-fbf4a0a5e4fa"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("8bacfc2c-633e-4eff-bbaa-d233916b8763"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("94ce7ba0-e73b-79ec-619d-c0817e296bb6"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("962ce353-613c-74db-83d5-cca35bede489"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("9cd2ed8d-098a-b2cb-dcde-d100b88dd3dd"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("9d17cb18-c8d3-c5c7-fe4c-3990d0dc1d6c"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4787bdf-b277-7fde-f8f1-1d2ce6babf6a"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a4a9a1a8-a7ee-047e-1753-d0ac5a1288fd"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a606681b-46b1-9faf-919d-a93a8b2e6fd3"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("a87325bb-8d0c-d832-fbf2-f6eb99c6d78f"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b1601569-da6c-a45a-f72d-0a0a801132cc"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b42c38ba-d9f9-6898-5c95-0572b8cc4461"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b4b1c616-ac3a-da52-8f53-3925ce003bf0"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b6083d70-d7bb-fc50-453f-e77883d22d15"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("b9627c9c-b4dc-2906-cfff-9a1b67fe7872"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("be4ff455-5e31-dc6c-4cf7-e87c01d4062b"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c4cb6ae6-7dc8-efcd-ceb5-2b68e6919110"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c77966c8-92ed-c0fa-396e-475f5da6bd88"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("c96f7062-79e3-928c-8ec2-f70182a33e27"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("ce8d4549-4834-494e-3d49-daf0e180e35e"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e226a779-586d-75f6-8704-4b06e8053001"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e81bf932-8829-70a6-e530-fdf18e26adf9"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("e8a096f2-7f6b-7a4f-8d39-69489afb69d9"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("eabbc77e-85be-2bb6-40c2-c4f647ac984f"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("ef9f35f5-5195-1954-e356-14d89b4dfad9"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f29f0509-a891-a240-ed5e-b635b29130bd"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f616d444-c35d-1e44-65c7-ce9ae64f734a"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f77c1d3f-d3c5-118f-1a0a-bab078be9695"));

            migrationBuilder.DeleteData(
                table: "Teams",
                keyColumn: "Id",
                keyValue: new Guid("f94609eb-94f8-c0ef-ca27-9935d5ce875f"));

            migrationBuilder.DropColumn(
                name: "Division",
                table: "Teams");
        }
    }
}
