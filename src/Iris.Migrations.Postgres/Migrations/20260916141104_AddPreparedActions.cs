using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreparedActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PlanSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ValidationSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    RequestedJobTemplateId = table.Column<int>(type: "integer", nullable: true),
                    RequestedInventory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedLimit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedCheckMode = table.Column<bool>(type: "boolean", nullable: false),
                    InstallationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CanceledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparedActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreparedActions_ApplicationInstallationId",
                table: "PreparedActions",
                column: "ApplicationInstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedActions_Status",
                table: "PreparedActions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreparedActions");
        }
    }
}
