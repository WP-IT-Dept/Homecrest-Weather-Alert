using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomecrestWeatherAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptOutSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OptOutSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPolledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptOutSyncStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptOutSyncStates");
        }
    }
}
