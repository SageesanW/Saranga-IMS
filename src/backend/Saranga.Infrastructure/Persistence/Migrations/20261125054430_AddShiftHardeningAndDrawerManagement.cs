using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftHardeningAndDrawerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Shifts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LKR");

            migrationBuilder.AddColumn<bool>(
                name: "IsOverrideClose",
                table: "Shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OverrideReason",
                table: "Shifts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsOverrideClose",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "OverrideReason",
                table: "Shifts");
        }
    }
}
