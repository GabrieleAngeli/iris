using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServerCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capabilities",
                table: "Servers",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ResourceCpuCores",
                table: "Servers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceDiskGb",
                table: "Servers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceMemoryMb",
                table: "Servers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsedPorts",
                table: "Servers",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capabilities",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ResourceCpuCores",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ResourceDiskGb",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ResourceMemoryMb",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "UsedPorts",
                table: "Servers");
        }
    }
}
