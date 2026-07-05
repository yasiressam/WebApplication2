using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplatesToSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentApprovedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentApprovedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentFormMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentFormTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRejectedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRejectedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRemovedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRemovedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSubmittedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSubmittedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BasicInfoApprovedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BasicInfoApprovedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BasicInfoRejectedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BasicInfoRejectedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DirectAssignmentMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DirectAssignmentTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PromotionApprovedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PromotionApprovedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PromotionRejectedMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PromotionRejectedTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentApprovedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentApprovedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentFormMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentFormTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentRejectedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentRejectedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentRemovedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentRemovedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentSubmittedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AssignmentSubmittedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BasicInfoApprovedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BasicInfoApprovedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BasicInfoRejectedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BasicInfoRejectedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "DirectAssignmentMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "DirectAssignmentTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromotionApprovedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromotionApprovedTitle",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromotionRejectedMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromotionRejectedTitle",
                table: "SiteSettings");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "887a49d3-7614-4bd8-84e1-d13f454e319a");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "10",
                column: "ConcurrencyStamp",
                value: "0426a2ac-6196-4b07-8e88-d71a2a64f5af");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "2d1f0260-448c-4fcb-b485-64d45d3fd05b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "b488d1aa-f024-44e0-9fd0-ecf7ef261c32");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "3a34e72c-a450-41b8-be14-ed661e1799d0");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "a5839ac4-cee9-4261-a0a9-e317c37df565");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "8e1df8ec-369f-4810-a21e-30845934ff91");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "63a13cc3-569b-4f33-8e82-f320fa268e18");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "b916431e-85d7-4f52-9af6-c8f9fb3eb344");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "95174884-6e95-4387-9423-097fbd35abe2");
        }
    }
}
