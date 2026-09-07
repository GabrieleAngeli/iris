using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpenBaoEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OpenBaoTokenSecretReference = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    OpenBaoMountPath = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OpenBaoUseKvV2 = table.Column<bool>(type: "INTEGER", nullable: false),
                    AwxEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AwxTokenSecretReference = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    AwxJobTemplateId = table.Column<int>(type: "INTEGER", nullable: true),
                    AnsibleEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AnsiblePlaybook = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    AnsibleInventory = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
