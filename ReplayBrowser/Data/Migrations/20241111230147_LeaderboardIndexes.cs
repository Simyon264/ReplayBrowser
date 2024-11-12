using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class LeaderboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Leaderboards_GeneratedAt",
                table: "Leaderboards",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Leaderboards_Servers",
                table: "Leaderboards",
                column: "Servers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leaderboards_GeneratedAt",
                table: "Leaderboards");

            migrationBuilder.DropIndex(
                name: "IX_Leaderboards_Servers",
                table: "Leaderboards");
        }
    }
}
