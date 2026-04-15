using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NimbosService.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilestoneAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneShards = table.Column<int>(type: "int", nullable: false),
                    Award1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Award2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Award3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimedAwardIndex = table.Column<int>(type: "int", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentViewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneAwards", x => x.Id);
                    table.CheckConstraint("CK_MilestoneAward_MilestoneShards", "[MilestoneShards] IN (12, 35, 50, 100)");
                    table.ForeignKey(
                        name: "FK_MilestoneAwards_Users_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneAwards_ChildId_MilestoneShards",
                table: "MilestoneAwards",
                columns: new[] { "ChildId", "MilestoneShards" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilestoneAwards");
        }
    }
}
