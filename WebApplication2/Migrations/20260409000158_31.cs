using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _31 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FederationName",
                table: "FederationMemberships");

            migrationBuilder.AddColumn<int>(
                name: "FederationDivisionId",
                table: "FederationMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FederationGroupId",
                table: "FederationMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FederationId",
                table: "FederationMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FederationSectionId",
                table: "FederationMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FederationDivisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FederationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederationDivisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederationDivisions_Federations_FederationId",
                        column: x => x.FederationId,
                        principalTable: "Federations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FederationSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FederationDivisionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederationSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederationSections_FederationDivisions_FederationDivisionId",
                        column: x => x.FederationDivisionId,
                        principalTable: "FederationDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FederationGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FederationSectionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederationGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederationGroups_FederationSections_FederationSectionId",
                        column: x => x.FederationSectionId,
                        principalTable: "FederationSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_FederationMemberships_FederationDivisionId",
                table: "FederationMemberships",
                column: "FederationDivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_FederationMemberships_FederationGroupId",
                table: "FederationMemberships",
                column: "FederationGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FederationMemberships_FederationId",
                table: "FederationMemberships",
                column: "FederationId");

            migrationBuilder.CreateIndex(
                name: "IX_FederationMemberships_FederationSectionId",
                table: "FederationMemberships",
                column: "FederationSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FederationDivisions_FederationId",
                table: "FederationDivisions",
                column: "FederationId");

            migrationBuilder.CreateIndex(
                name: "IX_FederationGroups_FederationSectionId",
                table: "FederationGroups",
                column: "FederationSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FederationSections_FederationDivisionId",
                table: "FederationSections",
                column: "FederationDivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FederationMemberships_FederationDivisions_FederationDivisionId",
                table: "FederationMemberships",
                column: "FederationDivisionId",
                principalTable: "FederationDivisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FederationMemberships_FederationGroups_FederationGroupId",
                table: "FederationMemberships",
                column: "FederationGroupId",
                principalTable: "FederationGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FederationMemberships_FederationSections_FederationSectionId",
                table: "FederationMemberships",
                column: "FederationSectionId",
                principalTable: "FederationSections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FederationMemberships_Federations_FederationId",
                table: "FederationMemberships",
                column: "FederationId",
                principalTable: "Federations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FederationMemberships_FederationDivisions_FederationDivisionId",
                table: "FederationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_FederationMemberships_FederationGroups_FederationGroupId",
                table: "FederationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_FederationMemberships_FederationSections_FederationSectionId",
                table: "FederationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_FederationMemberships_Federations_FederationId",
                table: "FederationMemberships");

            migrationBuilder.DropTable(
                name: "FederationGroups");

            migrationBuilder.DropTable(
                name: "FederationSections");

            migrationBuilder.DropTable(
                name: "FederationDivisions");

            migrationBuilder.DropIndex(
                name: "IX_FederationMemberships_FederationDivisionId",
                table: "FederationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_FederationMemberships_FederationGroupId",
                table: "FederationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_FederationMemberships_FederationId",
                table: "FederationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_FederationMemberships_FederationSectionId",
                table: "FederationMemberships");

            migrationBuilder.DropColumn(
                name: "FederationDivisionId",
                table: "FederationMemberships");

            migrationBuilder.DropColumn(
                name: "FederationGroupId",
                table: "FederationMemberships");

            migrationBuilder.DropColumn(
                name: "FederationId",
                table: "FederationMemberships");

            migrationBuilder.DropColumn(
                name: "FederationSectionId",
                table: "FederationMemberships");

            migrationBuilder.AddColumn<string>(
                name: "FederationName",
                table: "FederationMemberships",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "dcd5f518-01ed-4f4e-88a4-7527c7bd5de4");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "a2d86fc9-b094-4a39-a946-2ded04c6412e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "2639ef7d-077b-43e4-b3b7-7019d5ca7c52");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "f01d09f2-f882-423a-bdc1-6faa5b1e8d47");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "66a232e8-1fdc-48dc-9256-a59ac08baf0f");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "f618f274-c3eb-44b0-909a-3b5cd101c822");
        }
    }
}
