using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class ProfileDbCaching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerGuid = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    PlayerGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlayerDataId = table.Column<int>(type: "integer", nullable: false),
                    TotalEstimatedPlaytime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TotalRoundsPlayed = table.Column<int>(type: "integer", nullable: false),
                    TotalAntagRoundsPlayed = table.Column<int>(type: "integer", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsWatched = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.PlayerGuid);
                    table.ForeignKey(
                        name: "FK_PlayerProfiles_PlayerData_PlayerDataId",
                        column: x => x.PlayerDataId,
                        principalTable: "PlayerData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectedPlayerDataPlayerGuid = table.Column<Guid>(type: "uuid", nullable: true),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    LastPlayed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RoundsPlayed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterData_PlayerProfiles_CollectedPlayerDataPlayerGuid",
                        column: x => x.CollectedPlayerDataPlayerGuid,
                        principalTable: "PlayerProfiles",
                        principalColumn: "PlayerGuid");
                });

            migrationBuilder.CreateTable(
                name: "JobCountData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectedPlayerDataPlayerGuid = table.Column<Guid>(type: "uuid", nullable: true),
                    JobPrototype = table.Column<string>(type: "text", nullable: false),
                    RoundsPlayed = table.Column<int>(type: "integer", nullable: false),
                    LastPlayed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobCountData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobCountData_PlayerProfiles_CollectedPlayerDataPlayerGuid",
                        column: x => x.CollectedPlayerDataPlayerGuid,
                        principalTable: "PlayerProfiles",
                        principalColumn: "PlayerGuid");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterData_CollectedPlayerDataPlayerGuid",
                table: "CharacterData",
                column: "CollectedPlayerDataPlayerGuid");

            migrationBuilder.CreateIndex(
                name: "IX_JobCountData_CollectedPlayerDataPlayerGuid",
                table: "JobCountData",
                column: "CollectedPlayerDataPlayerGuid");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_PlayerDataId",
                table: "PlayerProfiles",
                column: "PlayerDataId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_PlayerGuid",
                table: "PlayerProfiles",
                column: "PlayerGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterData");

            migrationBuilder.DropTable(
                name: "JobCountData");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");

            migrationBuilder.DropTable(
                name: "PlayerData");
        }
    }
}
