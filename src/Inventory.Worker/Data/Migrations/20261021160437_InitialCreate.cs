using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Worker.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResponseMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponseType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResponseRoutingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResponsePayload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Disponible = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => x.Sku);
                    table.CheckConstraint("CK_Stock_Disponible_NoNegativo", "\"Disponible\" >= 0");
                });

            migrationBuilder.InsertData(
                table: "Stock",
                columns: new[] { "Sku", "Disponible" },
                values: new object[,]
                {
                    { "ABC-01", 100 },
                    { "ABC-02", 50 },
                    { "ABC-03", 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedEvents");

            migrationBuilder.DropTable(
                name: "Stock");
        }
    }
}
