using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAwxOAuthRefreshToIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwxOAuthClientId",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwxOAuthClientSecretReference",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwxRefreshTokenSecretReference",
                table: "IntegrationSettings",
                type: "TEXT",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwxOAuthClientId",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "AwxOAuthClientSecretReference",
                table: "IntegrationSettings");

            migrationBuilder.DropColumn(
                name: "AwxRefreshTokenSecretReference",
                table: "IntegrationSettings");
        }
    }
}
