using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReadyHampersAndOccasions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Occasions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Chip = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Lead = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Accent = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BloomyLink = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Occasions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadyHampers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ForWhom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BudgetVnd = table.Column<long>(type: "bigint", nullable: false),
                    PackagingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadyHampers", x => x.Id);
                    table.CheckConstraint("CK_ReadyHampers_BudgetVnd", "\"BudgetVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_ReadyHampers_Packagings_PackagingId",
                        column: x => x.PackagingId,
                        principalTable: "Packagings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadyHamperItems",
                columns: table => new
                {
                    ReadyHamperId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadyHamperItems", x => new { x.ReadyHamperId, x.ProductId });
                    table.CheckConstraint("CK_ReadyHamperItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_ReadyHamperItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadyHamperItems_ReadyHampers_ReadyHamperId",
                        column: x => x.ReadyHamperId,
                        principalTable: "ReadyHampers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadyHamperOccasions",
                columns: table => new
                {
                    HampersId = table.Column<int>(type: "integer", nullable: false),
                    OccasionsId = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadyHamperOccasions", x => new { x.HampersId, x.OccasionsId });
                    table.ForeignKey(
                        name: "FK_ReadyHamperOccasions_Occasions_OccasionsId",
                        column: x => x.OccasionsId,
                        principalTable: "Occasions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadyHamperOccasions_ReadyHampers_HampersId",
                        column: x => x.HampersId,
                        principalTable: "ReadyHampers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadyHamperItems_ProductId",
                table: "ReadyHamperItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadyHamperOccasions_OccasionsId",
                table: "ReadyHamperOccasions",
                column: "OccasionsId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadyHampers_PackagingId",
                table: "ReadyHampers",
                column: "PackagingId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadyHampers_Slug",
                table: "ReadyHampers",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadyHamperItems");

            migrationBuilder.DropTable(
                name: "ReadyHamperOccasions");

            migrationBuilder.DropTable(
                name: "Occasions");

            migrationBuilder.DropTable(
                name: "ReadyHampers");
        }
    }
}
