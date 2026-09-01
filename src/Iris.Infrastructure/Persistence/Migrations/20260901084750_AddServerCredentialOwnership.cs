using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServerCredentialOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ServerCredentials",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "SystemUser");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "ServerCredentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "ServerCredentials",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerCredentials_OwnerUserId",
                table: "ServerCredentials",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServerCredentials_OwnerUserId",
                table: "ServerCredentials");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ServerCredentials");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ServerCredentials");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "ServerCredentials");
        }
    }
}
