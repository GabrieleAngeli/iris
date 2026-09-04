using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationInstallations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUnitKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    InstallationProfileKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ServerNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationInstallationBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationInstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaceholderKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    TargetKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetSlug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ValuePreview = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallationBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallationBindings_ApplicationInstallations_ApplicationInstallationId",
                        column: x => x.ApplicationInstallationId,
                        principalTable: "ApplicationInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallationBindings_ApplicationInstallationId",
                table: "ApplicationInstallationBindings",
                column: "ApplicationInstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_ApplicationId",
                table: "ApplicationInstallations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_ApplicationVersionId",
                table: "ApplicationInstallations",
                column: "ApplicationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_ServerNodeId",
                table: "ApplicationInstallations",
                column: "ServerNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationInstallationBindings");

            migrationBuilder.DropTable(
                name: "ApplicationInstallations");
        }
    }
}
