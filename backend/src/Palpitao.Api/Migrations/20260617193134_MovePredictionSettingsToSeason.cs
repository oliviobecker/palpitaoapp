using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Api.Migrations
{
    /// <inheritdoc />
    public partial class MovePredictionSettingsToSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowParticipantsToSubmitPredictions",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "AllowParticipantsToViewOthersPredictions",
                table: "Groups");

            migrationBuilder.AddColumn<bool>(
                name: "AllowParticipantsToSubmitPredictions",
                table: "Seasons",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowParticipantsToViewOthersPredictions",
                table: "Seasons",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowParticipantsToSubmitPredictions",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "AllowParticipantsToViewOthersPredictions",
                table: "Seasons");

            migrationBuilder.AddColumn<bool>(
                name: "AllowParticipantsToSubmitPredictions",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowParticipantsToViewOthersPredictions",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Groups",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"),
                column: "AllowParticipantsToSubmitPredictions",
                value: true);
        }
    }
}
