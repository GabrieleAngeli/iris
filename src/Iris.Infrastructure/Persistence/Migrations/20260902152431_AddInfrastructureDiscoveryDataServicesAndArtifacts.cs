using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInfrastructureDiscoveryDataServicesAndArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MachineSize",
                table: "Servers",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsVersion",
                table: "Servers",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactFeed",
                table: "Applications",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactName",
                table: "Applications",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactPath",
                table: "Applications",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactProvider",
                table: "Applications",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildPipelineUrl",
                table: "Applications",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderApplicationSlug",
                table: "ApplicationDependencies",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPlaceholderKey",
                table: "ApplicationDependencies",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Size = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    StorageGb = table.Column<int>(type: "INTEGER", nullable: true),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataServices", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataServices");

            migrationBuilder.DropColumn(
                name: "MachineSize",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "OsVersion",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ArtifactFeed",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ArtifactName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ArtifactPath",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ArtifactProvider",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "BuildPipelineUrl",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ProviderApplicationSlug",
                table: "ApplicationDependencies");

            migrationBuilder.DropColumn(
                name: "ProviderPlaceholderKey",
                table: "ApplicationDependencies");
        }
    }
}
