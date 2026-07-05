using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppFieldsToIdentify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Identifies', 'IsWhatsAppVerified') IS NULL
                BEGIN
                    ALTER TABLE dbo.Identifies ADD IsWhatsAppVerified bit NOT NULL CONSTRAINT DF_Identifies_IsWhatsAppVerified DEFAULT(0)
                END

                IF COL_LENGTH('dbo.Identifies', 'WhatsAppNumber') IS NULL
                BEGIN
                    ALTER TABLE dbo.Identifies ADD WhatsAppNumber nvarchar(max) NULL
                END

                IF COL_LENGTH('dbo.Identifies', 'WhatsAppVerifiedAt') IS NULL
                BEGIN
                    ALTER TABLE dbo.Identifies ADD WhatsAppVerifiedAt datetime2 NULL
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Identifies', 'IsWhatsAppVerified') IS NOT NULL
                BEGIN
                    DECLARE @dfName nvarchar(128);
                    SELECT @dfName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t ON t.object_id = c.object_id
                    WHERE t.name = 'Identifies' AND c.name = 'IsWhatsAppVerified';

                    IF @dfName IS NOT NULL
                        EXEC('ALTER TABLE dbo.Identifies DROP CONSTRAINT [' + @dfName + ']');

                    ALTER TABLE dbo.Identifies DROP COLUMN IsWhatsAppVerified
                END

                IF COL_LENGTH('dbo.Identifies', 'WhatsAppNumber') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.Identifies DROP COLUMN WhatsAppNumber
                END

                IF COL_LENGTH('dbo.Identifies', 'WhatsAppVerifiedAt') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.Identifies DROP COLUMN WhatsAppVerifiedAt
                END
            ");
        }
    }
}
