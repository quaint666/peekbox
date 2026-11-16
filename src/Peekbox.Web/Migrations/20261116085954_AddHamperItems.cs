using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHamperItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HamperItems",
                columns: table => new
                {
                    HamperId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HamperItems", x => new { x.HamperId, x.ProductId });
                    table.CheckConstraint("CK_HamperItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_HamperItems_Hampers_HamperId",
                        column: x => x.HamperId,
                        principalTable: "Hampers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HamperItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HamperItems_ProductId",
                table: "HamperItems",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HamperItems");
        }
    }
}
