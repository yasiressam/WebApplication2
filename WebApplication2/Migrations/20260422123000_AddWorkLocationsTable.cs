using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    public partial class AddWorkLocationsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentifyId = table.Column<int>(type: "int", nullable: false),
                    Governorate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkLocations_Identifies_IdentifyId",
                        column: x => x.IdentifyId,
                        principalTable: "Identifies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkLocations_IdentifyId",
                table: "WorkLocations",
                column: "IdentifyId",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO WorkLocations (IdentifyId, Governorate, District)
                SELECT Id, WorkGovernorate, WorkDistrict
                FROM Identifies
                WHERE WorkGovernorate IS NOT NULL AND LTRIM(RTRIM(WorkGovernorate)) <> ''
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkLocations");
        }
    }
}
