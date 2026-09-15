using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpsHostAndAzureDevOpsRepoSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AzureDevOpsBranch",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOpsManifestPath",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOpsProject",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOpsRepository",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpsAwxRepoPath",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "/home/ops/Refactoring_ops_flow/awx");

            migrationBuilder.AddColumn<string>(
                name: "OpsHostAuthMethod",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "SshKey");

            migrationBuilder.AddColumn<string>(
                name: "OpsHostEndpoint",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpsHostPort",
                table: "IntegrationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 22);

            migrationBuilder.AddColumn<string>(
                name: "OpsHostSecretReference",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpsHostUsername",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzureDevOpsBranch",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "AzureDevOpsManifestPath",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "AzureDevOpsProject",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "AzureDevOpsRepository",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "OpsAwxRepoPath",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "OpsHostAuthMethod",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "OpsHostEndpoint",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "OpsHostPort",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "OpsHostSecretReference",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "OpsHostUsername",
                table: "IntegrationSettings");
        }
    }
}
