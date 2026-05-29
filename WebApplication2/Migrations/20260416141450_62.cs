using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _62 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeputyOfAssignmentId",
                table: "ManagementAssignments");

            migrationBuilder.DropColumn(
                name: "DeputyType",
                table: "ManagementAssignments");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRole",
                table: "ManagementAssignments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "a8d1a57e-f224-47c7-bcdd-d30d2318f24f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "c67422ac-f1fb-4903-925d-ae7d12dcca84");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "c8860f6b-042b-401e-b669-0eae1013920f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "ad395e63-7e34-44ad-a476-9c27efe8bafe");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "a340cec1-e1f4-4902-9281-c86b38838394");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "6a2ccd42-79bf-4df4-881a-2d1feb8a7afe");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "d8d734b6-83b0-4071-92d9-19c1be9dff70");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "8", "371e75e1-e7ad-4222-b884-bbdc19639f34", "Manager", "MANAGER" },
                    { "9", "b0aec482-7547-4fbe-8068-fb2c6bcff96e", "AssistantManager", "ASSISTANTMANAGER" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9");

            migrationBuilder.DropColumn(
                name: "AssignmentRole",
                table: "ManagementAssignments");

            migrationBuilder.AddColumn<int>(
                name: "DeputyOfAssignmentId",
                table: "ManagementAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeputyType",
                table: "ManagementAssignments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "6ef12ff2-fa18-4610-a6d5-7196d7ad9f71");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "293381a2-e5d8-4820-85dc-b1edbe807650");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "a32f4f55-5ccf-479f-a528-eb8c148000a0");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "693b1f20-17f5-40fc-8981-11752e2b9f29");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "6755fda9-3cce-4b6c-8d62-7d761797566b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "5c1d6e4a-d25a-4324-92ed-a0a35c038ea5");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "94b5ac62-01d4-445a-a807-8c326f108f1c");
        }
    }
}
