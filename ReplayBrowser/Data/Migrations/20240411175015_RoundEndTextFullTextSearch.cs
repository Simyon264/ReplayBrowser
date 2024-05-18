using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class RoundEndTextFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "RoundEndTextSearchVector",
                table: "Replays",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "RoundEndText" });

            migrationBuilder.CreateIndex(
                name: "IX_Replays_RoundEndTextSearchVector",
                table: "Replays",
                column: "RoundEndTextSearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Replays_RoundEndTextSearchVector",
                table: "Replays");

            migrationBuilder.DropColumn(
                name: "RoundEndTextSearchVector",
                table: "Replays");
        }
    }
}
