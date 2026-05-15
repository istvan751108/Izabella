using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class MilkDataStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkDataStagings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EarTag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BarnId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LactationNo = table.Column<int>(type: "int", nullable: false),
                    DaysInMilk = table.Column<int>(type: "int", nullable: false),
                    Enar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Yield1 = table.Column<double>(type: "float", nullable: true),
                    Yield2 = table.Column<double>(type: "float", nullable: true),
                    Yield3 = table.Column<double>(type: "float", nullable: true),
                    Fat1 = table.Column<double>(type: "float", nullable: true),
                    Fat2 = table.Column<double>(type: "float", nullable: true),
                    Fat3 = table.Column<double>(type: "float", nullable: true),
                    Protein1 = table.Column<double>(type: "float", nullable: true),
                    Protein2 = table.Column<double>(type: "float", nullable: true),
                    Protein3 = table.Column<double>(type: "float", nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkDataStagings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkDataStagings");
        }
    }
}
