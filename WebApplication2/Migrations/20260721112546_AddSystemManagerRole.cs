using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemManagerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "9ad869f3-8c81-463b-b03b-a290912e52b5");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "10",
                column: "ConcurrencyStamp",
                value: "38558075-29a8-4429-aabd-8b197456eaae");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "27871df1-295c-49e3-9571-d170054253cc");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "59e411d7-7f30-4b09-b1a7-3c7a3e95227a");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "3bddc7ec-8a18-41af-8e5d-a79df38754e2");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "5c6dc127-cca4-4818-a081-2776fbcc6f9c");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "5a86a0e8-ee92-4abf-855a-47a4872809e7");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "dabbdaa7-0d9a-4cbe-9eb8-b8aad2f5a2b8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "6d99b102-fdbc-4cc3-b0b7-7eeae1884108");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "2d8ea90f-b69b-465e-aa2a-a6206f621056");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { "11", "c67c5c29-c955-43ae-a66f-dceb9273764c", "SystemManager", "SYSTEMMANAGER" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "11");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "7607b759-6502-41cc-9ff2-13f64faf4add");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "10",
                column: "ConcurrencyStamp",
                value: "5314c5b8-7326-461e-a862-29e617d3908f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "56c697fd-9bda-4f86-a524-b826907926a0");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "89574893-31d7-4457-bbfc-f52237d8246d");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "e728041a-7e76-424a-9c60-06ff63d1edd8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "d15ccbea-faa6-4723-9510-5e624af52a7e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "d6f5be16-3fbc-4379-970a-60bcabcc6947");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "7",
                column: "ConcurrencyStamp",
                value: "98e36f86-6d7f-4df7-8fed-5f5c551f101b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8",
                column: "ConcurrencyStamp",
                value: "098c7fbc-018d-4ed1-9ffd-c399019115e4");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9",
                column: "ConcurrencyStamp",
                value: "de970c92-4116-4461-b201-7c22bb82cc8e");
        }
    }
}
