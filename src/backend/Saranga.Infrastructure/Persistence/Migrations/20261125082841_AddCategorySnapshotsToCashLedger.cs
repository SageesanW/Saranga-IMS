using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorySnapshotsToCashLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryCodeSnapshot",
                table: "CashLedgerTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryNameSnapshot",
                table: "CashLedgerTransactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryCodeSnapshot",
                table: "CashLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "CategoryNameSnapshot",
                table: "CashLedgerTransactions");
        }
    }
}
