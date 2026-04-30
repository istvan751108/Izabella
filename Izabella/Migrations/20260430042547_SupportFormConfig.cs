using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class SupportFormConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportFormConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicenseeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LicenseeClientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medication1Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medication1Agent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medication2Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medication2Agent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medication3Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medication3Agent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilingPlace = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportFormConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportFormConfigs");
        }
    }
}
