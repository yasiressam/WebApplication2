using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class _4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AffiliationEntity",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "Division",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "AffiliationInfos");

            migrationBuilder.AddColumn<int>(
                name: "AffiliationEntityId",
                table: "AffiliationInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DivisionId",
                table: "AffiliationInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "AffiliationInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "AffiliationInfos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AffiliationEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliationEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Divisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AffiliationEntityId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Divisions_AffiliationEntities_AffiliationEntityId",
                        column: x => x.AffiliationEntityId,
                        principalTable: "AffiliationEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "514063ef-e875-42cd-98ca-3835971262d1");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "9c6e0e9f-2f0e-4fce-87dc-6b0712847050");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "a4127bbd-2207-47d9-a9a3-56cef9680b3b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "f5df6358-4ad5-4f96-9037-68d3442c398e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "d7803d45-0fb1-4dae-a1c1-ae6a9b344b57");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "8dcf930e-4f2b-43f0-8575-007d09944704");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliationInfos_AffiliationEntityId",
                table: "AffiliationInfos",
                column: "AffiliationEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliationInfos_DivisionId",
                table: "AffiliationInfos",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliationInfos_GroupId",
                table: "AffiliationInfos",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliationInfos_SectionId",
                table: "AffiliationInfos",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_AffiliationEntityId",
                table: "Divisions",
                column: "AffiliationEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SectionId",
                table: "Groups",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DivisionId",
                table: "Sections",
                column: "DivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliationInfos_AffiliationEntities_AffiliationEntityId",
                table: "AffiliationInfos",
                column: "AffiliationEntityId",
                principalTable: "AffiliationEntities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliationInfos_Divisions_DivisionId",
                table: "AffiliationInfos",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliationInfos_Groups_GroupId",
                table: "AffiliationInfos",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliationInfos_Sections_SectionId",
                table: "AffiliationInfos",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AffiliationInfos_AffiliationEntities_AffiliationEntityId",
                table: "AffiliationInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_AffiliationInfos_Divisions_DivisionId",
                table: "AffiliationInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_AffiliationInfos_Groups_GroupId",
                table: "AffiliationInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_AffiliationInfos_Sections_SectionId",
                table: "AffiliationInfos");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Divisions");

            migrationBuilder.DropTable(
                name: "AffiliationEntities");

            migrationBuilder.DropIndex(
                name: "IX_AffiliationInfos_AffiliationEntityId",
                table: "AffiliationInfos");

            migrationBuilder.DropIndex(
                name: "IX_AffiliationInfos_DivisionId",
                table: "AffiliationInfos");

            migrationBuilder.DropIndex(
                name: "IX_AffiliationInfos_GroupId",
                table: "AffiliationInfos");

            migrationBuilder.DropIndex(
                name: "IX_AffiliationInfos_SectionId",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "AffiliationEntityId",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "AffiliationInfos");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "AffiliationInfos");

            migrationBuilder.AddColumn<string>(
                name: "AffiliationEntity",
                table: "AffiliationInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Division",
                table: "AffiliationInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "AffiliationInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "AffiliationInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1",
                column: "ConcurrencyStamp",
                value: "ec30b66f-549b-4aac-88b4-2556094f6799");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2",
                column: "ConcurrencyStamp",
                value: "74d66c63-f59f-46ad-9d03-89f5f35371a6");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3",
                column: "ConcurrencyStamp",
                value: "de2539a5-2dcc-4c78-8129-1d430dbdc4a7");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4",
                column: "ConcurrencyStamp",
                value: "642d5d7c-b8af-4798-9198-9bbd7724ed12");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5",
                column: "ConcurrencyStamp",
                value: "a1a55461-7169-4660-895e-ea68967e0359");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6",
                column: "ConcurrencyStamp",
                value: "4b20184e-2580-48b3-a19d-50d553a3499a");
        }
    }
}
