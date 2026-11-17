using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class HamperStartedFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StartedFromId",
                table: "Hampers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hampers_StartedFromId",
                table: "Hampers",
                column: "StartedFromId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hampers_ReadyHampers_StartedFromId",
                table: "Hampers",
                column: "StartedFromId",
                principalTable: "ReadyHampers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hampers_ReadyHampers_StartedFromId",
                table: "Hampers");

            migrationBuilder.DropIndex(
                name: "IX_Hampers_StartedFromId",
                table: "Hampers");

            migrationBuilder.DropColumn(
                name: "StartedFromId",
                table: "Hampers");
        }
    }
}
