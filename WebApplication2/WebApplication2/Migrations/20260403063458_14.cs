using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Union",
                table: "Union");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ngo",
                table: "Ngo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Federation",
                table: "Federation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Association",
                table: "Association");

            migrationBuilder.RenameTable(
                name: "Union",
                newName: "Unions");

            migrationBuilder.RenameTable(
                name: "Ngo",
                newName: "Ngos");

            migrationBuilder.RenameTable(
                name: "Federation",
                newName: "Federations");

            migrationBuilder.RenameTable(
                name: "Association",
                newName: "Associations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Unions",
                table: "Unions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ngos",
                table: "Ngos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Federations",
                table: "Federations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Associations",
                table: "Associations",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "57de7039-43c8-4888-9bb6-5c55d39d62fb");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "c8f05399-146d-4193-a8ce-f0d86697e180");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "14e28a38-c287-4bd9-abfd-3eb868062f05");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "a6a1300f-87ff-4319-ace7-88efe93c73b2");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "2ddfa473-72f4-4885-82c7-bb33d8cd3274");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "ab97ed65-79e5-4462-bc5d-e56dd7a40a96");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Unions",
                table: "Unions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ngos",
                table: "Ngos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Federations",
                table: "Federations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Associations",
                table: "Associations");

            migrationBuilder.RenameTable(
                name: "Unions",
                newName: "Union");

            migrationBuilder.RenameTable(
                name: "Ngos",
                newName: "Ngo");

            migrationBuilder.RenameTable(
                name: "Federations",
                newName: "Federation");

            migrationBuilder.RenameTable(
                name: "Associations",
                newName: "Association");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Union",
                table: "Union",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ngo",
                table: "Ngo",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Federation",
                table: "Federation",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Association",
                table: "Association",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "a157ebca-9d16-456f-b928-01195cd7031f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "222d6a23-e322-4e96-88c2-36c9e10ae921");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "0804afc1-f7d1-43c5-a946-c625d990be92");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "318e8507-ce33-4e24-a0f3-4749f93f6507");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "b1480826-ff47-434d-a092-bf83d0885aef");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "19c5021c-7b9e-4899-81ee-ae78cd359e09");
        }
    }
}
