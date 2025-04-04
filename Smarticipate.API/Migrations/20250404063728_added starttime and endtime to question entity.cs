using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smarticipate.API.Migrations
{
    /// <inheritdoc />
    public partial class addedstarttimeandendtimetoquestionentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TimeStamp",
                table: "Questions",
                newName: "StartTime");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "Questions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Questions");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "Questions",
                newName: "TimeStamp");
        }
    }
}
