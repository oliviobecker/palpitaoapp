using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupPredictionVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowParticipantsToViewOthersPredictions",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowParticipantsToViewOthersPredictions",
                table: "Groups");
        }
    }
}
