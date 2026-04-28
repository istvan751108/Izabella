using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class Pregnancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPregnancyTestDate",
                table: "Cattles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PregnancyStatus",
                table: "Cattles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPregnancyTestDate",
                table: "Cattles");

            migrationBuilder.DropColumn(
                name: "PregnancyStatus",
                table: "Cattles");
        }
    }
}
