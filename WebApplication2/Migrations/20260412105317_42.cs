using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _42 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetAgeFrom",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetAgeTo",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetDivisionId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetEducation",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetEmploymentStatus",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetGender",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetGroupId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetMaritalStatus",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetMinistry",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetSectionId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetStudyStage",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "TargetStudyType",
                table: "Events",
                newName: "Description");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "056067ad-bb48-4bf4-9ab2-49a5e17ccc86");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "07987c6c-1f19-4953-b49e-016c8bbc8d31");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "121233e1-d228-4959-bd91-817aa4fe2b1b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "1ab6b11d-483e-49a8-b79e-0c505e74ddcb");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "ec64bc8b-165a-4523-b662-5717dc74dd68");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "57f361fa-4371-48b5-be37-270579ca8594");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Events",
                newName: "TargetStudyType");

            migrationBuilder.AddColumn<int>(
                name: "TargetAgeFrom",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetAgeTo",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetDivisionId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetEducation",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetEmploymentStatus",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetGender",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetGroupId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetMaritalStatus",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetMinistry",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetSectionId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetStudyStage",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "73141876-a1a0-4b37-b941-5832ef37a906");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "fbcb5dfd-8551-4186-b86b-dc5095bde4f3");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "193a076e-d03b-4567-8b4c-a49148bc5091");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "61f5ea0a-3e30-445b-8db9-bde8d7a0d564");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "6aba9992-96af-45e8-a393-4fe1002d47cb");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "64667699-166c-46a8-a002-14853f60f16c");
        }
    }
}
