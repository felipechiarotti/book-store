using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookStore.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyBookCopyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "book_copies");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "book_copies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "book_copies");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "book_copies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
