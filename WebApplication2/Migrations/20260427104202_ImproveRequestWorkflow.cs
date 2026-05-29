using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class ImproveRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Requests', 'Priority') IS NULL
BEGIN
    ALTER TABLE dbo.Requests
    ADD Priority int NOT NULL CONSTRAINT DF_Requests_Priority DEFAULT(0)
END

IF COL_LENGTH('dbo.Requests', 'Type') IS NULL
BEGIN
    ALTER TABLE dbo.Requests
    ADD [Type] int NOT NULL CONSTRAINT DF_Requests_Type DEFAULT(0)
END");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "dc24a62b-0b24-4cde-ac1f-8e332c9620dc");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "3ab064b5-8f6c-4a01-bd83-58765df30920");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "e996d5bc-61e8-4999-b30d-15fcff9ac49e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "c1177365-ab4b-496d-870f-03fde8ba3ccc");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "63a001c6-3f12-45f9-a82f-dcef97cdd065");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "f72b8f48-fcc3-4262-8181-fbd6bfc564f3");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "92f04d04-d95a-4ea9-bbf7-43fccf8c1df6");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "ace11fdf-abf6-44ed-8d76-75da0b029cd3");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "6642d45d-98fe-4584-83ca-1f969c8aa50c");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Requests', 'Priority') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Requests DROP CONSTRAINT IF EXISTS DF_Requests_Priority
    ALTER TABLE dbo.Requests DROP COLUMN Priority
END

IF COL_LENGTH('dbo.Requests', 'Type') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Requests DROP CONSTRAINT IF EXISTS DF_Requests_Type
    ALTER TABLE dbo.Requests DROP COLUMN [Type]
END");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "adcd9d0a-3ac3-49a5-a845-84c2a616433f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "1c3ff95a-3783-4c86-91c7-15e836ba2da6");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "87af60e2-cf4c-4440-a7b7-f4a5445aa376");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "360203a0-863e-4b5f-8071-5f345f4331e8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "a5eac5b7-e1c4-4253-921f-430384fc9586");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "dd5efacf-439b-41f3-a416-b38b84dc7a92");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "c73b65e8-f177-4cf2-b1ce-c5cd29ff34be");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "0311e803-67a7-4145-a66e-3fc6866abc80");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "59707b5f-da6e-40e5-a643-a6bdbe05078d");
        }
    }
}
