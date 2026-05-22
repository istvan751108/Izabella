using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class MilkSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TruckPlateNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrailerPlateNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WeightKg = table.Column<double>(type: "float", nullable: false),
                    UnitPrice = table.Column<double>(type: "float", nullable: false),
                    EkaerNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    IsEkaerClosed = table.Column<bool>(type: "bit", nullable: false),
                    DailyDeliverySequence = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkSales", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkSales");
        }
    }
}
