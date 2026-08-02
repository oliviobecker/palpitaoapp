using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <summary>
    /// Adds the per-season FA Cup toggle. The <c>defaultValue</c> exists only to backfill
    /// the rows that already exist (every current season keeps the FA Cup on) and is dropped
    /// right after, so the column matches the model, which declares no store default.
    /// </summary>
    public partial class AddSeasonFaCupEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FaCupEnabled",
                table: "Seasons",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                ALTER TABLE "Seasons" ALTER COLUMN "FaCupEnabled" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaCupEnabled",
                table: "Seasons");
        }
    }
}
