using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLocalPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PasswordSetupPending",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordUpdatedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordSetupPending",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordUpdatedAtUtc",
                table: "Users");
        }
    }
}
