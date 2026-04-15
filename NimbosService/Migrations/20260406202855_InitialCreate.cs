using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NimbosService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Vibe = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ListPin = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoogleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    TotalStars = table.Column<int>(type: "int", nullable: false),
                    DailyStars = table.Column<int>(type: "int", nullable: false),
                    LastOpened = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletionPercentage = table.Column<double>(type: "float", nullable: false),
                    StarsLit = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailySnapshots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shields",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fragments = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shields", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Shields_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    IsSnoozed = table.Column<bool>(type: "bit", nullable: false),
                    IsDismissedToday = table.Column<bool>(type: "bit", nullable: false),
                    IsSkippedTomorrow = table.Column<bool>(type: "bit", nullable: false),
                    IsTomorrowOnly = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailySnapshots_UserId_SnapshotDate",
                table: "DailySnapshots",
                columns: new[] { "UserId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UserId",
                table: "Tasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeviceId",
                table: "Users",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "[GoogleId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySnapshots");

            migrationBuilder.DropTable(
                name: "Shields");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
