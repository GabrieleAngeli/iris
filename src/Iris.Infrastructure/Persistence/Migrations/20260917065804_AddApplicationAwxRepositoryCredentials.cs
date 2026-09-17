using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAwxRepositoryCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwxRepositoryEndpoint",
                table: "Applications",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwxRepositoryTokenSecretReference",
                table: "Applications",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwxRepositoryEndpoint",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AwxRepositoryTokenSecretReference",
                table: "Applications");
        }
    }
}
