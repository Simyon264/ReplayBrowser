using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class MarkAccountGuidUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_Guid",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Guid",
                table: "Accounts",
                column: "Guid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_Guid",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Guid",
                table: "Accounts",
                column: "Guid");
        }
    }
}
