using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EditLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HolderUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HolderDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RefreshedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditLocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditLocks_ResourceType_ResourceId",
                table: "EditLocks",
                columns: new[] { "ResourceType", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditLocks");
        }
    }
}
