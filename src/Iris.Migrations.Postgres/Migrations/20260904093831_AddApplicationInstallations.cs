using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUnitKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstallationProfileKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServerNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationInstallationBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaceholderKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValuePreview = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallationBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallationBindings_ApplicationInstallations_Ap~",
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
