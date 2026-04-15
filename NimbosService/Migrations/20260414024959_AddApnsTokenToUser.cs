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
            migrationBuilder.AddColumn<string>(
                name: "ApnsToken",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApnsToken",
                table: "Users");
        }
    }
}
