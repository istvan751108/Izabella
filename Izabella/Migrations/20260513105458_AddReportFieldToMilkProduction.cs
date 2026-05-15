using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddReportFieldToMilkProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkProductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CattleId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LactationNo = table.Column<int>(type: "int", nullable: false),
                    DaysInMilk = table.Column<int>(type: "int", nullable: false),
                    Yield1 = table.Column<double>(type: "float", nullable: false),
                    Yield2 = table.Column<double>(type: "float", nullable: false),
                    Yield3 = table.Column<double>(type: "float", nullable: false),
                    Fat1 = table.Column<double>(type: "float", nullable: true),
                    Fat2 = table.Column<double>(type: "float", nullable: true),
                    Fat3 = table.Column<double>(type: "float", nullable: true),
                    Protein1 = table.Column<double>(type: "float", nullable: true),
                    Protein2 = table.Column<double>(type: "float", nullable: true),
                    Protein3 = table.Column<double>(type: "float", nullable: true),
                    IsEstimated = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkProductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkProductions_Cattles_CattleId",
                        column: x => x.CattleId,
                        principalTable: "Cattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilkProductions_CattleId",
                table: "MilkProductions",
                column: "CattleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkProductions");
        }
    }
}
