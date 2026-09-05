using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <summary>
    /// Adds the per-season public standings link: the key itself and the flag that decides
    /// whether it resolves. The key is added nullable so existing rows can be backfilled with
    /// a <b>distinct</b> value each -- a constant default would collide the moment the unique
    /// index is created -- and only then made required. Publishing stays off for every
    /// existing season: deploying this must not expose anyone's data on its own.
    /// </summary>
    public partial class AddSeasonPublicKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicKey",
                table: "Seasons",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicStandingsEnabled",
                table: "Seasons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfills one random 12-char uppercase-hex key per existing season -- the same
            // shape PublicKeyGenerator mints, so the table holds a single key format.
            migrationBuilder.Sql("""
                UPDATE "Seasons"
                   SET "PublicKey" = upper(substr(md5(random()::text || clock_timestamp()::text || "Id"::text), 1, 12))
                 WHERE "PublicKey" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PublicKey",
                table: "Seasons",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_PublicKey",
                table: "Seasons",
                column: "PublicKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seasons_PublicKey",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PublicKey",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PublicStandingsEnabled",
                table: "Seasons");
        }
    }
}
