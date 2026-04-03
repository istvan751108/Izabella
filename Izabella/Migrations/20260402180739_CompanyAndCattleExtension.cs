using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class CompanyAndCattleExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Cattles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DamAgeAtCalving",
                table: "Cattles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAlive",
                table: "Cattles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTwin",
                table: "Cattles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HerdCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultPrefix = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnarPrefix = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cattles_CompanyId",
                table: "Cattles",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cattles_Companies_CompanyId",
                table: "Cattles",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cattles_Companies_CompanyId",
                table: "Cattles");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Cattles_CompanyId",
                table: "Cattles");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Cattles");

            migrationBuilder.DropColumn(
                name: "DamAgeAtCalving",
                table: "Cattles");

            migrationBuilder.DropColumn(
                name: "IsAlive",
                table: "Cattles");

            migrationBuilder.DropColumn(
                name: "IsTwin",
                table: "Cattles");
        }
    }
}
