using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class Coupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Money",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiscountVnd",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                table: "Hampers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false),
                    MinGoodsVnd = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    Uses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                    table.CheckConstraint("CK_Coupons_Value", "\"Value\" > 0 AND (\"Kind\" <> 'Percent' OR \"Value\" <= 100) AND \"MinGoodsVnd\" >= 0 AND \"Uses\" >= 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Money",
                table: "Orders",
                sql: "\"GoodsVnd\" >= 0 AND \"PackagingFeeVnd\" >= 0 AND \"DeliveryFeeVnd\" >= 0 AND \"DiscountVnd\" >= 0 AND \"DiscountVnd\" <= \"GoodsVnd\" AND \"TotalVnd\" = \"GoodsVnd\" + \"PackagingFeeVnd\" + \"DeliveryFeeVnd\" - \"DiscountVnd\"");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Money",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountVnd",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                table: "Hampers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Money",
                table: "Orders",
                sql: "\"GoodsVnd\" >= 0 AND \"PackagingFeeVnd\" >= 0 AND \"DeliveryFeeVnd\" >= 0 AND \"TotalVnd\" = \"GoodsVnd\" + \"PackagingFeeVnd\" + \"DeliveryFeeVnd\"");
        }
    }
}
