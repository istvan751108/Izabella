using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiquidManures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<double>(type: "float", nullable: false),
                    Cow = table.Column<double>(type: "float", nullable: false),
                    Young6_9 = table.Column<double>(type: "float", nullable: false),
                    Young9_12 = table.Column<double>(type: "float", nullable: false),
                    Young12Preg = table.Column<double>(type: "float", nullable: false),
                    PregnantHeifer = table.Column<double>(type: "float", nullable: false),
                    VoucherCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiquidManures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolidManureDailies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalNet = table.Column<double>(type: "float", nullable: false),
                    Cow = table.Column<double>(type: "float", nullable: false),
                    CalfMilk = table.Column<double>(type: "float", nullable: false),
                    Calf3_6 = table.Column<double>(type: "float", nullable: false),
                    Young6_9 = table.Column<double>(type: "float", nullable: false),
                    Young9_12 = table.Column<double>(type: "float", nullable: false),
                    PregnantHeifer = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolidManureDailies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolidManureLoads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LicensePlate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GrossWeight = table.Column<double>(type: "float", nullable: false),
                    TareWeight = table.Column<double>(type: "float", nullable: false),
                    NetWeight = table.Column<double>(type: "float", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolidManureLoads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolidManures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gross = table.Column<double>(type: "float", nullable: false),
                    Tare = table.Column<double>(type: "float", nullable: false),
                    Net = table.Column<double>(type: "float", nullable: false),
                    Cow = table.Column<double>(type: "float", nullable: false),
                    CalfMilk = table.Column<double>(type: "float", nullable: false),
                    Calf3_6 = table.Column<double>(type: "float", nullable: false),
                    Young6_9 = table.Column<double>(type: "float", nullable: false),
                    Young9_12 = table.Column<double>(type: "float", nullable: false),
                    PregnantHeifer = table.Column<double>(type: "float", nullable: false),
                    VoucherIn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VoucherOut = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolidManures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Warehouse = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiquidManures");

            migrationBuilder.DropTable(
                name: "SolidManureDailies");

            migrationBuilder.DropTable(
                name: "SolidManureLoads");

            migrationBuilder.DropTable(
                name: "SolidManures");

            migrationBuilder.DropTable(
                name: "Vouchers");
        }
    }
}
