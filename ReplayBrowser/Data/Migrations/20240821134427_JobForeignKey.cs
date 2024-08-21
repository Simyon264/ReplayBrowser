using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class JobForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EffectiveJobId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_EffectiveJobId",
                table: "Players",
                column: "EffectiveJobId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_JobDepartments_EffectiveJobId",
                table: "Players",
                column: "EffectiveJobId",
                principalTable: "JobDepartments",
                principalColumn: "Id");

            migrationBuilder.Sql(
                """
                UPDATE "Players"
                SET "EffectiveJobId" = (
                    SELECT j."Id" FROM "JobDepartments" j
                    WHERE "Players"."JobPrototypes"[1] = j."Job"
                )
                WHERE "Players"."JobPrototypes" != '{}'
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_JobDepartments_EffectiveJobId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_EffectiveJobId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EffectiveJobId",
                table: "Players");
        }
    }
}
