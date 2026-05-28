using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddGenomicTestingDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGenomicTested",
                table: "Cattles");

            migrationBuilder.AddColumn<DateTime>(
                name: "GenomicTestDate",
                table: "Cattles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenomicTestDate",
                table: "Cattles");

            migrationBuilder.AddColumn<bool>(
                name: "IsGenomicTested",
                table: "Cattles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
