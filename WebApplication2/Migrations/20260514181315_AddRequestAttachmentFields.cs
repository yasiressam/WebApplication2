using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestAttachmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Requests', 'AttachmentContentType') IS NULL
                BEGIN
                    ALTER TABLE dbo.Requests ADD AttachmentContentType nvarchar(120) NULL
                END

                IF COL_LENGTH('dbo.Requests', 'AttachmentFileName') IS NULL
                BEGIN
                    ALTER TABLE dbo.Requests ADD AttachmentFileName nvarchar(260) NULL
                END

                IF COL_LENGTH('dbo.Requests', 'AttachmentPath') IS NULL
                BEGIN
                    ALTER TABLE dbo.Requests ADD AttachmentPath nvarchar(max) NULL
                END

                IF COL_LENGTH('dbo.Requests', 'AttachmentSize') IS NULL
                BEGIN
                    ALTER TABLE dbo.Requests ADD AttachmentSize bigint NULL
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Requests', 'AttachmentContentType') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.Requests DROP COLUMN AttachmentContentType
                END

                IF COL_LENGTH('dbo.Requests', 'AttachmentFileName') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.Requests DROP COLUMN AttachmentFileName
                END

                IF COL_LENGTH('dbo.Requests', 'AttachmentPath') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.Requests DROP COLUMN AttachmentPath
                END

                IF COL_LENGTH('dbo.Requests', 'AttachmentSize') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.Requests DROP COLUMN AttachmentSize
                END
            ");
        }
    }
}
