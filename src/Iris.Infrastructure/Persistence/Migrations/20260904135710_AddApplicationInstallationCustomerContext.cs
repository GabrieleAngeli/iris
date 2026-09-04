using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationInstallationCustomerContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Environment",
                table: "ApplicationInstallations");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerContextId",
                table: "ApplicationInstallations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_CustomerContextId",
                table: "ApplicationInstallations",
                column: "CustomerContextId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationInstallations_CustomerContextId",
                table: "ApplicationInstallations");

            migrationBuilder.DropColumn(
                name: "CustomerContextId",
                table: "ApplicationInstallations");

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "ApplicationInstallations",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }
    }
}
