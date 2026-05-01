using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class Add2335meds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeiferMed1Agent",
                table: "SupportFormConfigs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeiferMed1Name",
                table: "SupportFormConfigs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeiferMed2Agent",
                table: "SupportFormConfigs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeiferMed2Name",
                table: "SupportFormConfigs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeiferMed3Agent",
                table: "SupportFormConfigs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeiferMed3Name",
                table: "SupportFormConfigs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeiferMed1Agent",
                table: "SupportFormConfigs");

            migrationBuilder.DropColumn(
                name: "HeiferMed1Name",
                table: "SupportFormConfigs");

            migrationBuilder.DropColumn(
                name: "HeiferMed2Agent",
                table: "SupportFormConfigs");

            migrationBuilder.DropColumn(
                name: "HeiferMed2Name",
                table: "SupportFormConfigs");

            migrationBuilder.DropColumn(
                name: "HeiferMed3Agent",
                table: "SupportFormConfigs");

            migrationBuilder.DropColumn(
                name: "HeiferMed3Name",
                table: "SupportFormConfigs");
        }
    }
}
