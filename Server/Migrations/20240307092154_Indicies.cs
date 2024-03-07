using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class Indicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Replays_Gamemode",
                table: "Replays",
                column: "Gamemode");

            migrationBuilder.CreateIndex(
                name: "IX_Replays_Map",
                table: "Replays",
                column: "Map");

            migrationBuilder.CreateIndex(
                name: "IX_Replays_RoundEndText",
                table: "Replays",
                column: "RoundEndText");

            migrationBuilder.CreateIndex(
                name: "IX_Replays_ServerId",
                table: "Replays",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Replays_ServerName",
                table: "Replays",
                column: "ServerName");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players",
                column: "PlayerGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerIcName",
                table: "Players",
                column: "PlayerIcName");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerOocName",
                table: "Players",
                column: "PlayerOocName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Replays_Gamemode",
                table: "Replays");

            migrationBuilder.DropIndex(
                name: "IX_Replays_Map",
                table: "Replays");

            migrationBuilder.DropIndex(
                name: "IX_Replays_RoundEndText",
                table: "Replays");

            migrationBuilder.DropIndex(
                name: "IX_Replays_ServerId",
                table: "Replays");

            migrationBuilder.DropIndex(
                name: "IX_Replays_ServerName",
                table: "Replays");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerIcName",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerOocName",
                table: "Players");
        }
    }
}
