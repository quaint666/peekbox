using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class AdminOrderNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingCode",
                table: "Orders",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_AdminNote",
                table: "Orders",
                sql: "\"AdminNote\" IS NULL OR char_length(\"AdminNote\") BETWEEN 1 AND 500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_TrackingCode",
                table: "Orders",
                sql: "\"TrackingCode\" IS NULL OR char_length(\"TrackingCode\") BETWEEN 1 AND 60");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_AdminNote",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_TrackingCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrackingCode",
                table: "Orders");
        }
    }
}
