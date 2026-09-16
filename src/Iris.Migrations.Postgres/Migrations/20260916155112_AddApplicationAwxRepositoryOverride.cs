using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAwxRepositoryOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwxRepositoryBranch",
                table: "Applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwxRepositoryName",
                table: "Applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwxRepositoryProject",
                table: "Applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwxRepositoryBranch",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AwxRepositoryName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AwxRepositoryProject",
                table: "Applications");
        }
    }
}
