using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddMilkQualityLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkQualityLabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DekadNumber = table.Column<int>(type: "int", nullable: false),
                    FatPercentage = table.Column<double>(type: "float", nullable: false),
                    ProteinPercentage = table.Column<double>(type: "float", nullable: false),
                    SomaticCellCount = table.Column<int>(type: "int", nullable: false),
                    BacteriaCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkQualityLabs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkQualityLabs");
        }
    }
}
