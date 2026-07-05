using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddRemainingNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminAssignedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdminAssignedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MapViewerAssignedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MapViewerAssignedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MemberAssignedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MemberAssignedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewsEditorAssignedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewsEditorAssignedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfileUpdatedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfileUpdatedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SuperAdminAssignedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SuperAdminAssignedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "493d00dc-ed04-4d86-8e0d-d745aada37bf");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "10",
                column: "ConcurrencyStamp",
                value: "841a8cda-ef6c-447e-869c-c57f7a2b30df");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "996eca4d-66c4-445c-bcd0-0fa4d7272570");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "a5e70917-720c-4dfb-b148-98bc581d681f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "d7275494-7a46-4b2f-ba26-6ad0fc888f6e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "ab158367-5148-45f7-b170-0465dd7fb676");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "90d21a86-8719-49fe-bb10-1fa964866f2b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "de3fc0d7-319d-4689-b9c4-4202c41b8a5e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "668ca379-b2c5-428f-82fd-378a14b6b820");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "c929bb63-406e-451b-b8be-1252dbce54e9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminAssignedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AdminAssignedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MapViewerAssignedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MapViewerAssignedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MemberAssignedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MemberAssignedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "NewsEditorAssignedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "NewsEditorAssignedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ProfileUpdatedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ProfileUpdatedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SuperAdminAssignedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SuperAdminAssignedTitle",
                table: "SiteSettings");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "153d0576-6b7d-48d8-aac9-aad9f6c08295");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "10",
                column: "ConcurrencyStamp",
                value: "f10eac8d-3a9f-462a-85d4-29f154486e9b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "6037cb84-e087-4738-8850-86476a7e0d57");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "29c134ed-eafc-4e1c-b57e-5a536e07e1fe");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "e9535608-7112-45f3-ba1c-4170da894612");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "b984bb95-f145-43ba-bcec-bdbfa767650d");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "4daf98b9-65f5-4e73-9245-ee2afce14800");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "0c6d749d-7093-4866-8771-c9ec172639d4");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "4fead231-b90c-4152-ae2b-80f086ea758f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "6d1821f1-16ed-433d-96c1-f3aa4ddbb6e1");
        }
    }
}
