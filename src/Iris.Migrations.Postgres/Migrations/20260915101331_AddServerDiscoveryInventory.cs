using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddServerDiscoveryInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReachable",
                table: "Servers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDiscoveredAtUtc",
                table: "Servers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDiscoveryError",
                table: "Servers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceFreeDiskGb",
                table: "Servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceFreeMemoryMb",
                table: "Servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwxFactsJobTemplateId",
                table: "IntegrationSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServerDisks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    MountPoint = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    FileSystem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    TotalGb = table.Column<int>(type: "integer", nullable: false),
                    FreeGb = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerDisks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerDisks_Servers_ServerNodeId",
                        column: x => x.ServerNodeId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerDisks_ServerNodeId",
                table: "ServerDisks",
                column: "ServerNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerDisks");

            migrationBuilder.DropColumn(
                name: "IsReachable",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "LastDiscoveredAtUtc",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "LastDiscoveryError",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ResourceFreeDiskGb",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ResourceFreeMemoryMb",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "AwxFactsJobTemplateId",
                table: "IntegrationSettings");
        }
    }
}
