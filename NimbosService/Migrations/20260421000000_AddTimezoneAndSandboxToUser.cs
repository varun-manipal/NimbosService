using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NimbosService.Migrations
{
    /// <inheritdoc />
    public partial class AddTimezoneAndSandboxToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Users') AND name = N'Timezone'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [Timezone] nvarchar(64) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Users') AND name = N'ApnsSandbox'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [ApnsSandbox] bit NOT NULL DEFAULT 1;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Users') AND name = N'Timezone'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [Timezone];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Users') AND name = N'ApnsSandbox'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [ApnsSandbox];
                END
            ");
        }
    }
}
