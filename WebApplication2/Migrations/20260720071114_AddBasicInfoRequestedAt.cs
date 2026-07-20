using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicInfoRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BasicInfoRequestedAt",
                table: "Identifies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "fc934eef-d8e6-45eb-bd0f-f973213a7464");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "10",
                column: "ConcurrencyStamp",
                value: "c0cb7405-708e-432c-9b02-669af3ddbc03");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "960a03ea-d492-475e-8328-4c82c4824a40");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "f9bccf8d-f2e5-451a-9720-a10ab6248b47");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "11abe1d1-644e-4a2b-a4b6-a3a690776e41");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "08ec3dfa-d5df-48e7-a366-92d6454f91a8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "66fc9801-6931-435c-971e-fda2be74c23b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "5f1b9ba7-afd0-4fce-ac86-68fc956ef9cc");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "51ca6268-7d27-4b8e-a52d-be8c94daace5");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "10204b62-704f-478f-bf77-ed3416f577a4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasicInfoRequestedAt",
                table: "Identifies");

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
    }
}
