using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddServerCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Capabilities",
                table: "Servers",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "ResourceCpuCores",
                table: "Servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceDiskGb",
                table: "Servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceMemoryMb",
                table: "Servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "UsedPorts",
                table: "Servers",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);
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
