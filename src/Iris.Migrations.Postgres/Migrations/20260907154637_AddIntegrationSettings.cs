using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenBaoEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenBaoTokenSecretReference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    OpenBaoMountPath = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OpenBaoUseKvV2 = table.Column<bool>(type: "boolean", nullable: false),
                    AwxEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AwxTokenSecretReference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    AwxJobTemplateId = table.Column<int>(type: "integer", nullable: true),
                    AnsibleEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnsiblePlaybook = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    AnsibleInventory = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationSettings");
        }
    }
}
