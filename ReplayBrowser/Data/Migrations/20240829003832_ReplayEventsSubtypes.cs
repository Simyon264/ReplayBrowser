using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplayEventsSubtypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReplayDbEvent_ReplayEventPlayer_OriginId",
                table: "ReplayDbEvent");

            migrationBuilder.DropForeignKey(
                name: "FK_ReplayDbEvent_ReplayEventPlayer_TargetId",
                table: "ReplayDbEvent");

            migrationBuilder.DropIndex(
                name: "IX_ReplayDbEvent_OriginId",
                table: "ReplayDbEvent");

            migrationBuilder.DropIndex(
                name: "IX_ReplayDbEvent_TargetId",
                table: "ReplayDbEvent");

            migrationBuilder.RenameColumn(
                name: "TargetId",
                table: "ReplayDbEvent",
                newName: "Tier");

            migrationBuilder.RenameColumn(
                name: "OriginId",
                table: "ReplayDbEvent",
                newName: "ObjectsSold");

            migrationBuilder.AlterColumn<string>(
                name: "PlayerOocName",
                table: "ReplayEventPlayer",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlayerIcName",
                table: "ReplayEventPlayer",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerGuid",
                table: "ReplayEventPlayer",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string[]>(
                name: "JobPrototypes",
                table: "ReplayEventPlayer",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0],
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<string[]>(
                name: "AntagPrototypes",
                table: "ReplayEventPlayer",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0],
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlertLevel",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Amount",
                table: "ReplayDbEvent",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Buyer",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateVacuum",
                table: "ReplayDbEvent",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatMessageReplayEvent_Message",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatMessageReplayEvent_Sender",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cost",
                table: "ReplayDbEvent",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Countdown",
                table: "ReplayDbEvent",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discipline",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenericPlayerEvent_Origin",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenericPlayerEvent_Target",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Intensity",
                table: "ReplayDbEvent",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Item",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTileBreak",
                table: "ReplayDbEvent",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "MaxTileIntensity",
                table: "ReplayDbEvent",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobStateChangedNPCReplayEvent_Target",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "MobStateChangedPlayerReplayEvent_NewState",
                table: "ReplayDbEvent",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "MobStateChangedPlayerReplayEvent_OldState",
                table: "ReplayDbEvent",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobStateChangedPlayerReplayEvent_Target",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "NewState",
                table: "ReplayDbEvent",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "OldState",
                table: "ReplayDbEvent",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Products",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplayExplosionEvent_Type",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sender",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ShareTime",
                table: "ReplayDbEvent",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShuttleReplayEvent_Source",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Slope",
                table: "ReplayDbEvent",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Target",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "TileBreakScale",
                table: "ReplayDbEvent",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ReplayDbEvent",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertLevel",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Buyer",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "CanCreateVacuum",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "ChatMessageReplayEvent_Message",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "ChatMessageReplayEvent_Sender",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Countdown",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Discipline",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "GenericPlayerEvent_Origin",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "GenericPlayerEvent_Target",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Intensity",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Item",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "MaxTileBreak",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "MaxTileIntensity",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "MobStateChangedNPCReplayEvent_Target",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "MobStateChangedPlayerReplayEvent_NewState",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "MobStateChangedPlayerReplayEvent_OldState",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "MobStateChangedPlayerReplayEvent_Target",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "NewState",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "OldState",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Player",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Products",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "ReplayExplosionEvent_Type",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Sender",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "ShareTime",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "ShuttleReplayEvent_Source",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Slope",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "TileBreakScale",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ReplayDbEvent");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ReplayDbEvent");

            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "ReplayDbEvent",
                newName: "TargetId");

            migrationBuilder.RenameColumn(
                name: "ObjectsSold",
                table: "ReplayDbEvent",
                newName: "OriginId");

            migrationBuilder.AlterColumn<string>(
                name: "PlayerOocName",
                table: "ReplayEventPlayer",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PlayerIcName",
                table: "ReplayEventPlayer",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerGuid",
                table: "ReplayEventPlayer",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string[]>(
                name: "JobPrototypes",
                table: "ReplayEventPlayer",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string[]>(
                name: "AntagPrototypes",
                table: "ReplayEventPlayer",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_OriginId",
                table: "ReplayDbEvent",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_TargetId",
                table: "ReplayDbEvent",
                column: "TargetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReplayDbEvent_ReplayEventPlayer_OriginId",
                table: "ReplayDbEvent",
                column: "OriginId",
                principalTable: "ReplayEventPlayer",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReplayDbEvent_ReplayEventPlayer_TargetId",
                table: "ReplayDbEvent",
                column: "TargetId",
                principalTable: "ReplayEventPlayer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
