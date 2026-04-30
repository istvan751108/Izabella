using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerToHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "AnimalHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HerdId",
                table: "AnimalHistories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalHistories_CustomerId",
                table: "AnimalHistories",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalHistories_HerdId",
                table: "AnimalHistories",
                column: "HerdId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalHistories_Customers_CustomerId",
                table: "AnimalHistories",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalHistories_Herds_HerdId",
                table: "AnimalHistories",
                column: "HerdId",
                principalTable: "Herds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnimalHistories_Customers_CustomerId",
                table: "AnimalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_AnimalHistories_Herds_HerdId",
                table: "AnimalHistories");

            migrationBuilder.DropIndex(
                name: "IX_AnimalHistories_CustomerId",
                table: "AnimalHistories");

            migrationBuilder.DropIndex(
                name: "IX_AnimalHistories_HerdId",
                table: "AnimalHistories");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "AnimalHistories");

            migrationBuilder.DropColumn(
                name: "HerdId",
                table: "AnimalHistories");
        }
    }
}
