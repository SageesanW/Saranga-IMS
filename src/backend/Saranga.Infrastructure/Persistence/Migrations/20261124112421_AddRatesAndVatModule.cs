using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRatesAndVatModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetalRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RatePerGram = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalReferenceRate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsOverride = table.Column<bool>(type: "boolean", nullable: false),
                    OverrideReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalRates_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalRates_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalRates_Purities_PurityId",
                        column: x => x.PurityId,
                        principalTable: "Purities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalRates_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VatConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RatePercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CalculationMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VatConfigurations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VatConfigurations_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VatConfigurations_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_BranchId_MaterialId_PurityId_CurrencyCode",
                table: "MetalRates",
                columns: new[] { "BranchId", "MaterialId", "PurityId", "CurrencyCode" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_CreatedAt",
                table: "MetalRates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_CreatedBy",
                table: "MetalRates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_EffectiveFrom",
                table: "MetalRates",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_MaterialId",
                table: "MetalRates",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_PurityId",
                table: "MetalRates",
                column: "PurityId");

            migrationBuilder.CreateIndex(
                name: "IX_VatConfigurations_BranchId",
                table: "VatConfigurations",
                column: "BranchId",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_VatConfigurations_CreatedBy",
                table: "VatConfigurations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VatConfigurations_EffectiveFrom",
                table: "VatConfigurations",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_VatConfigurations_UpdatedBy",
                table: "VatConfigurations",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetalRates");

            migrationBuilder.DropTable(
                name: "VatConfigurations");
        }
    }
}
