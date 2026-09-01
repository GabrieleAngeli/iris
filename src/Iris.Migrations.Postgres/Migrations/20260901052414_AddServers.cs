using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Hostname = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    Os = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HostingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublicIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    PrivateIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecretReference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerCredentials_Servers_ServerNodeId",
                        column: x => x.ServerNodeId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerCredentials_ServerNodeId_Username",
                table: "ServerCredentials",
                columns: new[] { "ServerNodeId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerCredentials");

            migrationBuilder.DropTable(
                name: "Servers");
        }
    }
}
