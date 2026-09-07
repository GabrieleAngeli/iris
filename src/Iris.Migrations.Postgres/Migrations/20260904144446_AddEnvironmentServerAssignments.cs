using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentServerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnvironmentServerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentServerAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentServerAssignments_CustomerContextId_ServerNodeId",
                table: "EnvironmentServerAssignments",
                columns: new[] { "CustomerContextId", "ServerNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentServerAssignments_ServerNodeId",
                table: "EnvironmentServerAssignments",
                column: "ServerNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvironmentServerAssignments");
        }
    }
}
