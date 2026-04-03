using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class CattleModuleInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cattles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EarTag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnarNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PassportNumber = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    PassportSequence = table.Column<int>(type: "int", nullable: false),
                    HerdCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BirthWeight = table.Column<double>(type: "float", nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    MotherEnar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FatherKlsz = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExitDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExitType = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cattles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BreedingDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CattleId = table.Column<int>(type: "int", nullable: false),
                    LastInseminationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SireKlsz = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PregnancyTestDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPregnant = table.Column<bool>(type: "bit", nullable: true),
                    AbortionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingDatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreedingDatas_Cattles_CattleId",
                        column: x => x.CattleId,
                        principalTable: "Cattles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingDatas_CattleId",
                table: "BreedingDatas",
                column: "CattleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreedingDatas");

            migrationBuilder.DropTable(
                name: "Cattles");
        }
    }
}
