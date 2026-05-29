using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppFieldsToIdentify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWhatsAppVerified",
                table: "Identifies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Identifies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppVerifiedAt",
                table: "Identifies",
                type: "datetime2",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWhatsAppVerified",
                table: "Identifies");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Identifies");

            migrationBuilder.DropColumn(
                name: "WhatsAppVerifiedAt",
                table: "Identifies");

        }
    }
}
