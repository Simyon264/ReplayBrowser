using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplayParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Replays_ReplayId",
                table: "Players");

            migrationBuilder.AlterColumn<int>(
                name: "ReplayId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ReplayParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReplayParticipants_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplayParticipants_PlayerGuid_ReplayId",
                table: "ReplayParticipants",
                columns: new[] { "PlayerGuid", "ReplayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReplayParticipants_ReplayId",
                table: "ReplayParticipants",
                column: "ReplayId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Replays_ReplayId",
                table: "Players",
                column: "ReplayId",
                principalTable: "Replays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                """
                insert into "ReplayParticipants" ("PlayerGuid", "ReplayId") (
                    select DISTINCT p."PlayerGuid", p."ReplayId"
                    from "Players" p
                    GROUP BY p."PlayerGuid", p."ReplayId"
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Replays_ReplayId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "ReplayParticipants");

            migrationBuilder.AlterColumn<int>(
                name: "ReplayId",
                table: "Players",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Replays_ReplayId",
                table: "Players",
                column: "ReplayId",
                principalTable: "Replays",
                principalColumn: "Id");
        }
    }
}
