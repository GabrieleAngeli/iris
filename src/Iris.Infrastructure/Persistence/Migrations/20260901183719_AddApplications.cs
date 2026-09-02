using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RuntimeType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RepositoryUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RuntimeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PreferredOs = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RequiredCpuCores = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiredMemoryMb = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiredPorts = table.Column<string>(type: "TEXT", nullable: false),
                    ImportWarnings = table.Column<string>(type: "TEXT", nullable: false),
                    RawImportPackageJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastImportSchemaVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastImportedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationVersions_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationConfigurationKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    TargetKind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    Secret = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PlaceholderKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationConfigurationKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationConfigurationKeys_ApplicationVersions_ApplicationVersionId",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PlaceholderKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDependencies_ApplicationVersions_ApplicationVersionId",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationPlaceholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPlaceholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationPlaceholders_ApplicationVersions_ApplicationVersionId",
                        column: x => x.ApplicationVersionId,
                        principalTable: "ApplicationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConfigurationKeys_ApplicationVersionId",
                table: "ApplicationConfigurationKeys",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDependencies_ApplicationVersionId",
                table: "ApplicationDependencies",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPlaceholders_ApplicationVersionId",
                table: "ApplicationPlaceholders",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationVersions_ApplicationId_Version",
                table: "ApplicationVersions",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Slug",
                table: "Applications",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationConfigurationKeys");

            migrationBuilder.DropTable(
                name: "ApplicationDependencies");

            migrationBuilder.DropTable(
                name: "ApplicationPlaceholders");

            migrationBuilder.DropTable(
                name: "ApplicationVersions");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
