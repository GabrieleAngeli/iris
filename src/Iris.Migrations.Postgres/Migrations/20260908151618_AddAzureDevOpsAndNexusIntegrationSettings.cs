using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddAzureDevOpsAndNexusIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AzureDevOpsEndpoint",
                table: "IntegrationSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOpsTokenSecretReference",
                table: "IntegrationSettings",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NexusEndpoint",
                table: "IntegrationSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NexusTokenSecretReference",
                table: "IntegrationSettings",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzureDevOpsEndpoint",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "AzureDevOpsTokenSecretReference",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "NexusEndpoint",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "NexusTokenSecretReference",
                table: "IntegrationSettings");
        }
    }
}
