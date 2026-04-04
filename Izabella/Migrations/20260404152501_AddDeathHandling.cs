using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddDeathHandling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeathLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CattleId = table.Column<int>(type: "int", nullable: false),
                    DeathDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedWeight = table.Column<double>(type: "float", nullable: false),
                    EarTagAtDeath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnarNumberAtDeath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnarReported = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeathLogs_Cattles_CattleId",
                        column: x => x.CattleId,
                        principalTable: "Cattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeathReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathReasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeathLogs_CattleId",
                table: "DeathLogs",
                column: "CattleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeathLogs");

            migrationBuilder.DropTable(
                name: "DeathReasons");
        }
    }
}
