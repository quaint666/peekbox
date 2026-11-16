using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Peekbox.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackagingId",
                table: "Hampers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Hampers_PackagingId",
                table: "Hampers",
                column: "PackagingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hampers_Packagings_PackagingId",
                table: "Hampers",
                column: "PackagingId",
                principalTable: "Packagings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hampers_Packagings_PackagingId",
                table: "Hampers");

            migrationBuilder.DropTable(
                name: "Packagings");

            migrationBuilder.DropIndex(
                name: "IX_Hampers_PackagingId",
                table: "Hampers");

            migrationBuilder.DropColumn(
                name: "PackagingId",
                table: "Hampers");
        }
    }
}
