using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NimbosService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFamilyInviteCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Families_InviteCode",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Families");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: This rollback cannot safely restore the unique index on a live database
            // that already has multiple Family rows — backfilling "" would violate uniqueness.
            // The column is restored as nullable with no index; repopulate values manually
            // before re-enabling the index if a rollback is truly needed.
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Families",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                defaultValue: null);
        }
    }
}
