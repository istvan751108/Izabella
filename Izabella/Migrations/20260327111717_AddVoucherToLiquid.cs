using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherToLiquid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VoucherCode",
                table: "LiquidManures",
                newName: "VoucherOut");

            migrationBuilder.AddColumn<string>(
                name: "VoucherIn",
                table: "LiquidManures",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoucherIn",
                table: "LiquidManures");

            migrationBuilder.RenameColumn(
                name: "VoucherOut",
                table: "LiquidManures",
                newName: "VoucherCode");
        }
    }
}
