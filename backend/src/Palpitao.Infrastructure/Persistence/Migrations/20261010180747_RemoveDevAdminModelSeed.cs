using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palpitao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The development admin and its default-group membership leave the model's seed data (they
    /// are seeded at runtime in Development now — see <c>DevelopmentAdmin</c>). Hand-edited to do
    /// nothing to the data: EF scaffolded a <c>DeleteData</c> for both rows, but on an existing
    /// database those rows are the real admin account and its membership (production's included),
    /// so they stay. <c>Down</c> is empty for the same reason — re-inserting rows that were never
    /// deleted would clash on their primary keys. This migration only moves the snapshot forward.
    /// </summary>
    public partial class RemoveDevAdminModelSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
