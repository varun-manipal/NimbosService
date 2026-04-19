using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NimbosService.Migrations
{
    /// <inheritdoc />
    public partial class AddApnsTokenToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Users') AND name = N'ApnsToken'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [ApnsToken] nvarchar(100) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Users') AND name = N'ApnsToken'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [ApnsToken];
                END
            ");
        }
    }
}
