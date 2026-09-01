using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    HolderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HolderDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefreshedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
