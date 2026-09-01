using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIsProvisioned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProvisioned",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProvisioned",
                table: "Users");
        }
    }
}
