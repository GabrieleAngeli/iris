using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
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
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsVersion",
                table: "Servers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactFeed",
                table: "Applications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactName",
                table: "Applications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactPath",
                table: "Applications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactProvider",
                table: "Applications",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildPipelineUrl",
                table: "Applications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderApplicationSlug",
                table: "ApplicationDependencies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPlaceholderKey",
                table: "ApplicationDependencies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Size = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StorageGb = table.Column<int>(type: "integer", nullable: true),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
