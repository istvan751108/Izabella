using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddMilkDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkCompanyDistributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistributionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DistributedKg = table.Column<double>(type: "float", nullable: false),
                    DistributedLiter = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkCompanyDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkCompanyDistributions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilkCompanyDistributions_CompanyId",
                table: "MilkCompanyDistributions",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkCompanyDistributions");
        }
    }
}
