using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplayEventsFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplayEventPlayer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReplayEventPlayer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AntagPrototypes = table.Column<string[]>(type: "text[]", nullable: false),
                    JobPrototypes = table.Column<string[]>(type: "text[]", nullable: false),
                    PlayerGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerIcName = table.Column<string>(type: "text", nullable: false),
                    PlayerOocName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayEventPlayer", x => x.Id);
                });
        }
    }
}
