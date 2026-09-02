using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddServerDiskReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResourceApplicationDiskGb",
                table: "Servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceBackupDiskGb",
                table: "Servers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResourceApplicationDiskGb",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ResourceBackupDiskGb",
                table: "Servers");
        }
    }
}
