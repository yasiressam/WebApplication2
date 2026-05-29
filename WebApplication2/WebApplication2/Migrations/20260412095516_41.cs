using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _41 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MeetingLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsUrgent = table.Column<bool>(type: "bit", nullable: false),
                    MaxAttendance = table.Column<int>(type: "int", nullable: true),
                    Governorate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetAffiliationEntityId = table.Column<int>(type: "int", nullable: true),
                    TargetDivisionId = table.Column<int>(type: "int", nullable: true),
                    TargetSectionId = table.Column<int>(type: "int", nullable: true),
                    TargetGroupId = table.Column<int>(type: "int", nullable: true),
                    TargetEducation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetStudyStage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetStudyType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetEmploymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetMinistry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetGender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetMaritalStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetAgeFrom = table.Column<int>(type: "int", nullable: true),
                    TargetAgeTo = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "ab51ca8d-8288-40cc-b0b8-8025cdced029");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "dd912263-dd3b-4420-80b9-1229ed0fc3aa");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "79d87d91-150b-4eb8-ab89-9e5ddbe14ce4");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "20e02db3-4ceb-4572-804c-0109ee470278");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "239dc0e6-1015-476c-bb57-3d24f020113f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "4f05662c-dc6a-4bbc-973d-d3c0609c762f");
        }
    }
}
