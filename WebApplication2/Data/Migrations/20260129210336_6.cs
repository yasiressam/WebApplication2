using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Data.Migrations
{
    /// <inheritdoc />
    public partial class _6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Identifies_Addresses_AddressId",
                table: "Identifies");

            migrationBuilder.DropForeignKey(
                name: "FK_Identifies_AspNetUsers_UserId",
                table: "Identifies");

            migrationBuilder.DropIndex(
                name: "IX_Identifies_AddressId",
                table: "Identifies");

            migrationBuilder.DropIndex(
                name: "IX_Identifies_UserId",
                table: "Identifies");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Identifies");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Identifies",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Identifies",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Identifies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Identifies_AddressId",
                table: "Identifies",
                column: "AddressId",
                unique: true,
                filter: "[AddressId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Identifies_UserId",
                table: "Identifies",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Identifies_Addresses_AddressId",
                table: "Identifies",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Identifies_AspNetUsers_UserId",
                table: "Identifies",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
