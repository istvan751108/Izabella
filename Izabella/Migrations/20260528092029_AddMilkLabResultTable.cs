using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddMilkLabResultTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkLabResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CattleId = table.Column<int>(type: "int", nullable: false),
                    Megye = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Tenyeszet = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Telep = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BefDat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BefDatTol = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModDat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllKod = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    NapiTej = table.Column<double>(type: "float", nullable: false),
                    NapiZsir = table.Column<double>(type: "float", nullable: false),
                    NapiFeherje = table.Column<double>(type: "float", nullable: false),
                    SzomatikusSejtszam = table.Column<int>(type: "int", nullable: false),
                    Karbamid = table.Column<double>(type: "float", nullable: false),
                    EllMod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AzTipus = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Tej1 = table.Column<double>(type: "float", nullable: false),
                    Zsir1 = table.Column<double>(type: "float", nullable: false),
                    Feherje1 = table.Column<double>(type: "float", nullable: false),
                    Cukor1 = table.Column<double>(type: "float", nullable: false),
                    Vonalkod1 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Idopont1 = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tej2 = table.Column<double>(type: "float", nullable: false),
                    Zsir2 = table.Column<double>(type: "float", nullable: false),
                    Feherje2 = table.Column<double>(type: "float", nullable: false),
                    Cukor2 = table.Column<double>(type: "float", nullable: false),
                    Vonalkod2 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Idopont2 = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tej3 = table.Column<double>(type: "float", nullable: false),
                    Zsir3 = table.Column<double>(type: "float", nullable: false),
                    Feherje3 = table.Column<double>(type: "float", nullable: false),
                    Cukor3 = table.Column<double>(type: "float", nullable: false),
                    Vonalkod3 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Idopont3 = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tej4 = table.Column<double>(type: "float", nullable: false),
                    Zsir4 = table.Column<double>(type: "float", nullable: false),
                    Feherje4 = table.Column<double>(type: "float", nullable: false),
                    Cukor4 = table.Column<double>(type: "float", nullable: false),
                    Vonalkod4 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Idopont4 = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tej5 = table.Column<double>(type: "float", nullable: false),
                    Zsir5 = table.Column<double>(type: "float", nullable: false),
                    Feherje5 = table.Column<double>(type: "float", nullable: false),
                    Cukor5 = table.Column<double>(type: "float", nullable: false),
                    Vonalkod5 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Idopont5 = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tej6 = table.Column<double>(type: "float", nullable: false),
                    Zsir6 = table.Column<double>(type: "float", nullable: false),
                    Feherje6 = table.Column<double>(type: "float", nullable: false),
                    Cukor6 = table.Column<double>(type: "float", nullable: false),
                    Vonalkod6 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Idopont6 = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkLabResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkLabResults_Cattles_CattleId",
                        column: x => x.CattleId,
                        principalTable: "Cattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilkLabResults_CattleId",
                table: "MilkLabResults",
                column: "CattleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkLabResults");
        }
    }
}
