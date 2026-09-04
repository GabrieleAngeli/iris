using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PersistApplicationManifestSemanticsEf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionTargetsJson",
                table: "ApplicationVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCpuCores",
                table: "ApplicationVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumMemoryMb",
                table: "ApplicationVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsSupportJson",
                table: "ApplicationVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortKeysJson",
                table: "ApplicationVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemSchemaJson",
                table: "ApplicationConfigurationKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "ApplicationConfigurationKeys",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileDefaultsJson",
                table: "ApplicationConfigurationKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilesJson",
                table: "ApplicationConfigurationKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionJson",
                table: "ApplicationConfigurationKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "ApplicationConfigurationKeys",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerializationJson",
                table: "ApplicationConfigurationKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValueType",
                table: "ApplicationConfigurationKeys",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationDependencyConstraints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaceholderKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ServiceKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VersionExpression = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDependencyConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDependencyConstraints_ApplicationVersions_Applic~",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationInstallationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Multiple = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationKeysJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallationProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallationProfiles_ApplicationVersions_Applica~",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntryPoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ArtifactPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExecutionTargetsJson = table.Column<string>(type: "text", nullable: true),
                    ProfilesJson = table.Column<string>(type: "text", nullable: true)
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
