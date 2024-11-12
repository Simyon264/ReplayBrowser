using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class LeaderboardRangeOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RangeOption",
                table: "Leaderboards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Leaderboards_RangeOption",
                table: "Leaderboards",
                column: "RangeOption");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leaderboards_RangeOption",
                table: "Leaderboards");

            migrationBuilder.DropColumn(
                name: "RangeOption",
                table: "Leaderboards");
        }
    }
}
