using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class FinalFixForHerdRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HerdCode",
                table: "Cattles",
                newName: "AgeGroup");

            migrationBuilder.AlterColumn<string>(
                name: "EnarPrefix",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPrefix",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "CurrentHerdId",
                table: "Cattles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Herds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HerdCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DefaultPrefix = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnarPrefix = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Herds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Herds_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cattles_CurrentHerdId",
                table: "Cattles",
                column: "CurrentHerdId");

            migrationBuilder.CreateIndex(
                name: "IX_Herds_CompanyId",
                table: "Herds",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cattles_Herds_CurrentHerdId",
                table: "Cattles",
                column: "CurrentHerdId",
                principalTable: "Herds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cattles_Herds_CurrentHerdId",
                table: "Cattles");

            migrationBuilder.DropTable(
                name: "Herds");

            migrationBuilder.DropIndex(
                name: "IX_Cattles_CurrentHerdId",
                table: "Cattles");

            migrationBuilder.DropColumn(
                name: "CurrentHerdId",
                table: "Cattles");

            migrationBuilder.RenameColumn(
                name: "AgeGroup",
                table: "Cattles",
                newName: "HerdCode");

            migrationBuilder.AlterColumn<string>(
                name: "EnarPrefix",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPrefix",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
