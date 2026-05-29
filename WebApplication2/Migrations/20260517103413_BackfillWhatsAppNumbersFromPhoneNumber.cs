using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class BackfillWhatsAppNumbersFromPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Identifies]
                SET [WhatsAppNumber] = [PhoneNumber]
                WHERE ([WhatsAppNumber] IS NULL OR LTRIM(RTRIM([WhatsAppNumber])) = '')
                  AND [PhoneNumber] IS NOT NULL
                  AND LTRIM(RTRIM([PhoneNumber])) <> ''
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
