using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplayEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReplayEventPlayer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerOocName = table.Column<string>(type: "text", nullable: true),
                    PlayerIcName = table.Column<string>(type: "text", nullable: true),
                    PlayerGuid = table.Column<Guid>(type: "uuid", nullable: true),
                    JobPrototypes = table.Column<string[]>(type: "text[]", nullable: true),
                    AntagPrototypes = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayEventPlayer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplayDbEvent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReplayId = table.Column<int>(type: "integer", nullable: false),
                    ClassType = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: true),
                    OriginId = table.Column<int>(type: "integer", nullable: true),
                    Time = table.Column<double>(type: "double precision", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    NearestBeacon = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<NpgsqlPoint>(type: "point", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayDbEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReplayDbEvent_ReplayEventPlayer_OriginId",
                        column: x => x.OriginId,
                        principalTable: "ReplayEventPlayer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReplayDbEvent_ReplayEventPlayer_TargetId",
                        column: x => x.TargetId,
                        principalTable: "ReplayEventPlayer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReplayDbEvent_Replays_ReplayId",
                        column: x => x.ReplayId,
                        principalTable: "Replays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_EventType",
                table: "ReplayDbEvent",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_OriginId",
                table: "ReplayDbEvent",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_ReplayId",
                table: "ReplayDbEvent",
                column: "ReplayId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_Severity",
                table: "ReplayDbEvent",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_TargetId",
                table: "ReplayDbEvent",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayDbEvent_Time",
                table: "ReplayDbEvent",
                column: "Time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplayDbEvent");

            migrationBuilder.DropTable(
                name: "ReplayEventPlayer");
        }
    }
}
