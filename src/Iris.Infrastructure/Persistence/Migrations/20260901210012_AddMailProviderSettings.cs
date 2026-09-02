using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iris.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMailProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailProviderSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUsername = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SmtpPasswordSecretReference = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    FromAddress = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    FromDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EnableSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailProviderSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailProviderSettings");
        }
    }
}
