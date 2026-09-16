using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationInstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PlanSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedJobTemplateId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestedInventory = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RequestedLimit = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RequestedCheckMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallationRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CanceledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
