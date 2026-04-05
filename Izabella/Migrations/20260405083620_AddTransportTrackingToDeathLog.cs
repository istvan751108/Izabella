using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Izabella.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportTrackingToDeathLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTransported",
                table: "DeathLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransportDate",
                table: "DeathLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportReceiptNumber",
                table: "DeathLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTransported",
                table: "DeathLogs");

            migrationBuilder.DropColumn(
                name: "TransportDate",
                table: "DeathLogs");

            migrationBuilder.DropColumn(
                name: "TransportReceiptNumber",
                table: "DeathLogs");
        }
    }
}
