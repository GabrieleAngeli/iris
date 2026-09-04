using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistApplicationManifestSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionTargetsJson",
                table: "ApplicationVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCpuCores",
                table: "ApplicationVersions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumMemoryMb",
                table: "ApplicationVersions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsSupportJson",
                table: "ApplicationVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortKeysJson",
                table: "ApplicationVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemSchemaJson",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileDefaultsJson",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilesJson",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionJson",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerializationJson",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValueType",
                table: "ApplicationConfigurationKeys",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationDependencyConstraints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaceholderKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ServiceKind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    VersionExpression = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDependencyConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDependencyConstraints_ApplicationVersions_ApplicationVersionId",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationInstallationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    Multiple = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigurationKeysJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallationProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallationProfiles_ApplicationVersions_ApplicationVersionId",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EntryPoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ArtifactPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ExecutionTargetsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ProfilesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationUnits_ApplicationVersions_ApplicationVersionId",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDependencyConstraints_ApplicationVersionId",
                table: "ApplicationDependencyConstraints",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallationProfiles_ApplicationVersionId",
                table: "ApplicationInstallationProfiles",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallationProfiles_ApplicationVersionId_Key",
                table: "ApplicationInstallationProfiles",
                columns: new[] { "ApplicationVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUnits_ApplicationVersionId",
                table: "ApplicationUnits",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUnits_ApplicationVersionId_Key",
                table: "ApplicationUnits",
                columns: new[] { "ApplicationVersionId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationDependencyConstraints");

            migrationBuilder.DropTable(
                name: "ApplicationInstallationProfiles");

            migrationBuilder.DropTable(
                name: "ApplicationUnits");

            migrationBuilder.DropColumn(
                name: "ExecutionTargetsJson",
                table: "ApplicationVersions");

            migrationBuilder.DropColumn(
                name: "MinimumCpuCores",
                table: "ApplicationVersions");

            migrationBuilder.DropColumn(
                name: "MinimumMemoryMb",
                table: "ApplicationVersions");

            migrationBuilder.DropColumn(
                name: "OsSupportJson",
                table: "ApplicationVersions");

            migrationBuilder.DropColumn(
                name: "PortKeysJson",
                table: "ApplicationVersions");

            migrationBuilder.DropColumn(
                name: "ItemSchemaJson",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "ProfileDefaultsJson",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "ProfilesJson",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "ResolutionJson",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "SerializationJson",
                table: "ApplicationConfigurationKeys");

            migrationBuilder.DropColumn(
                name: "ValueType",
                table: "ApplicationConfigurationKeys");
        }
    }
}
