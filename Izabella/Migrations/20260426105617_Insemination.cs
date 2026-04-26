using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class Insemination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InseminationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CattleEarTag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BullSemenId = table.Column<int>(type: "int", nullable: false),
                    InseminatorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarkerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsReInsemination = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InseminationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InseminationLogs_BullSemens_BullSemenId",
                        column: x => x.BullSemenId,
                        principalTable: "BullSemens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InseminationLogs_BullSemenId",
                table: "InseminationLogs",
                column: "BullSemenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InseminationLogs");
        }
    }
}
