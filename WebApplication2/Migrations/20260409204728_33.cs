using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _33 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RationCenter",
                table: "Identifies");

            migrationBuilder.DropColumn(
                name: "RationN",
                table: "Identifies");

            migrationBuilder.RenameColumn(
                name: "SubDistrict",
                table: "Addresses",
                newName: "Area");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityCardN",
                table: "Identifies",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "JobGrade",
                table: "Identifies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "Identifies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "662b5954-9ea6-4db8-9616-5f546323f95f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "5eb876fd-ddc6-4871-b01a-9d56bf53fd7d");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "9f665c2f-93df-446d-9e6d-7077c96648b1");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "16a174a1-70b9-4375-8f57-4d1d0475f764");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "73774c7e-e849-495b-a0d6-d264f31451cc");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "5eb075f6-6428-48f4-b2d9-92e0b21faa9c");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobGrade",
                table: "Identifies");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "Identifies");

            migrationBuilder.RenameColumn(
                name: "Area",
                table: "Addresses",
                newName: "SubDistrict");

            migrationBuilder.AlterColumn<int>(
                name: "IdentityCardN",
                table: "Identifies",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AddColumn<int>(
                name: "RationCenter",
                table: "Identifies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RationN",
                table: "Identifies",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "6e8d86c9-df3b-49d8-8236-70ed166d3e66");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "e956a500-4e36-409f-a18e-071c85a373d8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "17c1b56f-2e94-4d60-b982-16d48f801bad");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "349d8934-754d-4cc9-97c6-27a83803cc3c");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "45027cb2-ccdd-4c49-ae90-69d16e36b44b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "a839807f-69e0-48b8-b486-bc7083ce2a0a");
        }
    }
}
