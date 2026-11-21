using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class CardMessageAndHidePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardMessage",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HidePrices",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_CardMessage",
                table: "Orders",
                sql: "\"CardMessage\" IS NULL OR char_length(\"CardMessage\") BETWEEN 1 AND 200");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_CardMessage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CardMessage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HidePrices",
                table: "Orders");
        }
    }
}
