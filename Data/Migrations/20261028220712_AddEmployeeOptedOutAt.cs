using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomecrestWeatherAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeOptedOutAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OptedOutAt",
                table: "Employees",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptedOutAt",
                table: "Employees");
        }
    }
}
