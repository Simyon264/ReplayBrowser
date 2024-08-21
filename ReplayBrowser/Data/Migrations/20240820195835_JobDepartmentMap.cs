using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class JobDepartmentMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Job = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDepartments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobDepartments_Job",
                table: "JobDepartments",
                column: "Job",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "JobDepartments"
                ( "Job", "Department" )
                VALUES
                ( 'Captain', 'Command' ),
                ( 'HeadOfPersonnel', 'Command' ),
                ( 'ChiefMedicalOfficer', 'Command' ),
                ( 'ResearchDirector', 'Command' ),
                ( 'HeadOfSecurity', 'Command' ),
                ( 'ChiefEngineer', 'Command' ),
                ( 'Quartermaster', 'Command' ),
                ( 'Borg', 'Science' ),
                ( 'Scientist', 'Science' ),
                ( 'ResearchAssistant', 'Science' ),
                ( 'Warden', 'Security' ),
                ( 'Detective', 'Security' ),
                ( 'SecurityOfficer', 'Security' ),
                ( 'SecurityCadet', 'Security' ),
                ( 'MedicalDoctor', 'Medical' ),
                ( 'Chemist', 'Medical' ),
                ( 'Paramedic', 'Medical' ),
                ( 'Psychologist', 'Medical' ),
                ( 'MedicalIntern', 'Medical' ),
                ( 'StationEngineer', 'Engineering' ),
                ( 'AtmosphericTechnician', 'Engineering' ),
                ( 'TechnicalAssistant', 'Engineering' ),
                ( 'Janitor', 'Service' ),
                ( 'Chef', 'Service' ),
                ( 'Botanist', 'Service' ),
                ( 'Bartender', 'Service' ),
                ( 'Chaplain', 'Service' ),
                ( 'Lawyer', 'Service' ),
                ( 'Musician', 'Service' ),
                ( 'Reporter', 'Service' ),
                ( 'Zookeeper', 'Service' ),
                ( 'Librarian', 'Service' ),
                ( 'ServiceWorker', 'Service' ),
                ( 'Clown', 'Service' ),
                ( 'Mime', 'Service' ),
                ( 'CargoTechnician', 'Cargo' ),
                ( 'SalvageSpecialist', 'Cargo' ),
                ( 'Passenger', 'The tide' );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobDepartments");
        }
    }
}
