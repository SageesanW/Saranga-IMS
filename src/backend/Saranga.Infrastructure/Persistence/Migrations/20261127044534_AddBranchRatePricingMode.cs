using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchRatePricingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RatePricingMode",
                table: "Branches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "MANUAL");

            migrationBuilder.AlterColumn<decimal>(
                name: "KaratValueSnapshot",
                table: "InvoiceLines",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatePricingMode",
                table: "Branches");

            migrationBuilder.AlterColumn<int>(
                name: "KaratValueSnapshot",
                table: "InvoiceLines",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
