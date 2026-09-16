using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationRunOutcomeCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ElapsedSeconds",
                table: "InstallationRuns",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Output",
                table: "InstallationRuns",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstallationRuns_Status",
                table: "InstallationRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstallationRuns_Status",
                table: "InstallationRuns");

            migrationBuilder.DropColumn(
                name: "ElapsedSeconds",
                table: "InstallationRuns");

            migrationBuilder.DropColumn(
                name: "Output",
                table: "InstallationRuns");
        }
    }
}
