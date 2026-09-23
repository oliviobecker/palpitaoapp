using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundPart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rounds_SeasonId_Number",
                table: "Rounds");

            migrationBuilder.AddColumn<int>(
                name: "Part",
                table: "Rounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_SeasonId_Number_Part",
                table: "Rounds",
                columns: new[] { "SeasonId", "Number", "Part" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fails while any round is split into parts: the parts share a Number, which the
            // old (SeasonId, Number) index forbids. Ungroup them first.
            migrationBuilder.DropIndex(
                name: "IX_Rounds_SeasonId_Number_Part",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "Part",
                table: "Rounds");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_SeasonId_Number",
                table: "Rounds",
                columns: new[] { "SeasonId", "Number" },
                unique: true);
        }
    }
}
