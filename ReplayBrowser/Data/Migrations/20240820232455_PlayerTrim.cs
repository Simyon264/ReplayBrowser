using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class PlayerTrim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FIXME: Merge with previous migration

            migrationBuilder.Sql(
                """
                TRUNCATE "ReplayParticipants";
                """
            );

            migrationBuilder.AlterColumn<string>(
                name: "Link",
                table: "Replays",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParticipantId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Players_ParticipantId",
                table: "Players",
                column: "ParticipantId");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "ReplayParticipants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayParticipants_Username",
                table: "ReplayParticipants",
                column: "Username");

            // SQL magic here

            migrationBuilder.Sql(
                """
                insert into "ReplayParticipants" ("PlayerGuid", "ReplayId", "Username") (
                    select DISTINCT p."PlayerGuid", p."ReplayId", p."PlayerOocName"
                    from "Players" p
                    GROUP BY p."PlayerGuid", p."ReplayId", p."PlayerOocName"
                );
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE "Players" AS pl
                SET "ParticipantId" = p."Id"
                FROM "ReplayParticipants" AS p
                WHERE p."ReplayId" = pl."ReplayId"
                    AND p."PlayerGuid" = pl."PlayerGuid";
                """
            );

            // Data must be valid by this point

            migrationBuilder.AddForeignKey(
                name: "FK_Players_ReplayParticipants_ParticipantId",
                table: "Players",
                column: "ParticipantId",
                principalTable: "ReplayParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Replays_ReplayId",
                table: "Players");

            // Data lossy operations

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerOocName",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_ReplayId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlayerGuid",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlayerOocName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ReplayId",
                table: "Players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_ReplayParticipants_ParticipantId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_ReplayParticipants_Username",
                table: "ReplayParticipants");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "ReplayParticipants");

            migrationBuilder.RenameColumn(
                name: "ParticipantId",
                table: "Players",
                newName: "ReplayId");

            migrationBuilder.RenameIndex(
                name: "IX_Players_ParticipantId",
                table: "Players",
                newName: "IX_Players_ReplayId");

            migrationBuilder.AlterColumn<string>(
                name: "Link",
                table: "Replays",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerGuid",
                table: "Players",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PlayerOocName",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players",
                column: "PlayerGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerOocName",
                table: "Players",
                column: "PlayerOocName");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Replays_ReplayId",
                table: "Players",
                column: "ReplayId",
                principalTable: "Replays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
