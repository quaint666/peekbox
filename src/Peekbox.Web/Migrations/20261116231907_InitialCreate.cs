using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packagings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FeeVnd = table.Column<long>(type: "bigint", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packagings", x => x.Id);
                    table.CheckConstraint("CK_Packagings_FeeVnd", "\"FeeVnd\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ring = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.Id);
                    table.CheckConstraint("CK_Wards_Ring", "\"Ring\" BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeightGrams = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_PriceVnd", "\"PriceVnd\" > 0");
                    table.CheckConstraint("CK_Products_Stock", "\"Stock\" >= 0");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SenderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SenderPhone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SenderEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientPhone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    WardName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryWindow = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GoodsVnd = table.Column<long>(type: "bigint", nullable: false),
                    PackagingName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PackagingFeeVnd = table.Column<long>(type: "bigint", nullable: false),
                    DeliveryFeeVnd = table.Column<long>(type: "bigint", nullable: false),
                    TotalVnd = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Money", "\"GoodsVnd\" >= 0 AND \"PackagingFeeVnd\" >= 0 AND \"DeliveryFeeVnd\" >= 0 AND \"TotalVnd\" = \"GoodsVnd\" + \"PackagingFeeVnd\" + \"DeliveryFeeVnd\"");
                    table.CheckConstraint("CK_Orders_Phones", "\"SenderPhone\" ~ '^0[0-9]{9}$' AND \"RecipientPhone\" ~ '^0[0-9]{9}$'");
                    table.ForeignKey(
                        name: "FK_Orders_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Hampers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BudgetVnd = table.Column<long>(type: "bigint", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PackagingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hampers", x => x.Id);
                    table.CheckConstraint("CK_Hampers_BudgetVnd", "\"BudgetVnd\" >= 0");
                    table.ForeignKey(
                        name: "FK_Hampers_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hampers_Packagings_PackagingId",
                        column: x => x.PackagingId,
                        principalTable: "Packagings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Origin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeightGrams = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitPriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_OrderItems_UnitPriceVnd", "\"UnitPriceVnd\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Hampers_OrderId",
                table: "Hampers",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hampers_PackagingId",
                table: "Hampers",
                column: "PackagingId");

            migrationBuilder.CreateIndex(
                name: "IX_Hampers_Token",
                table: "Hampers",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryDate",
                table: "Orders",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Token",
                table: "Orders",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WardId",
                table: "Orders",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wards_Name",
                table: "Wards",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HamperItems");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Hampers");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Packagings");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Wards");
        }
    }
}
