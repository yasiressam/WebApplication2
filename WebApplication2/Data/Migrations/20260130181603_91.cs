using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Data.Migrations
{
    /// <inheritdoc />
    public partial class _91 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Link1Title",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Link1Url",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Link2Title",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Link2Url",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Link3Title",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Link3Url",
                table: "SiteSettings");

            migrationBuilder.RenameColumn(
                name: "TwitterUrl",
                table: "SiteSettings",
                newName: "WhatsAppNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WhatsAppNumber",
                table: "SiteSettings",
                newName: "TwitterUrl");

            migrationBuilder.AddColumn<string>(
                name: "Link1Title",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Link1Url",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Link2Title",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Link2Url",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Link3Title",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Link3Url",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
