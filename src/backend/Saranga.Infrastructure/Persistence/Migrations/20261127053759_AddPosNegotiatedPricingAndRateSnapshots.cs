using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPosNegotiatedPricingAndRateSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CombinedAgreedTotal",
                table: "Invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CombinedExpectedTotal",
                table: "Invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceAdjustmentAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvoicePriceConfirmed",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedInvoiceAdjustment",
                table: "InvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MakingChargeMethodSnapshot",
                table: "InvoiceLines",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "MakingChargeValueSnapshot",
                table: "InvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "MetalRateId",
                table: "InvoiceLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetLineTotal",
                table: "InvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuotedAt",
                table: "InvoiceLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RateEffectiveAt",
                table: "InvoiceLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateSource",
                table: "InvoiceLines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombinedAgreedTotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CombinedExpectedTotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceAdjustmentAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsInvoicePriceConfirmed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AllocatedInvoiceAdjustment",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "MakingChargeMethodSnapshot",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "MakingChargeValueSnapshot",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "MetalRateId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "NetLineTotal",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "QuotedAt",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "RateEffectiveAt",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "RateSource",
                table: "InvoiceLines");
        }
    }
}
