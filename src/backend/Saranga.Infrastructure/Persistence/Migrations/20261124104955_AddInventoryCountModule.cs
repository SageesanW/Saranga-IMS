using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCountModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReconciledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpectedItemCount = table.Column<int>(type: "integer", nullable: false),
                    CountedExpectedItemCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedCount = table.Column<int>(type: "integer", nullable: false),
                    MissingCount = table.Column<int>(type: "integer", nullable: false),
                    UnexpectedCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCounts_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCountLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedStockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagNumberSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BarcodeSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaterialNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PurityNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrossWeightSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StoneWeightSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetWeightSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PieceCountSnapshot = table.Column<int>(type: "integer", nullable: false),
                    ExpectedStatusSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsExpected = table.Column<bool>(type: "boolean", nullable: false),
                    IsCounted = table.Column<bool>(type: "boolean", nullable: false),
                    CountedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CountedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    ResolutionStatus = table.Column<int>(type: "integer", nullable: false),
                    ResolutionReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_Branches_ExpectedBranchId",
                        column: x => x.ExpectedBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_InventoryCounts_InventoryCountId",
                        column: x => x.InventoryCountId,
                        principalTable: "InventoryCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_StockLocations_ExpectedStockLocationId",
                        column: x => x.ExpectedStockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCountUnexpectedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedStockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentSystemStockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagNumberSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BarcodeSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaterialNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PurityNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrentSystemLocationNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrossWeightSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StoneWeightSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetWeightSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PieceCountSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CurrentSystemStatusSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScannedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionStatus = table.Column<int>(type: "integer", nullable: false),
                    ResolutionReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCountUnexpectedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCountUnexpectedItems_Branches_ObservedBranchId",
                        column: x => x.ObservedBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountUnexpectedItems_InventoryCounts_InventoryCoun~",
                        column: x => x.InventoryCountId,
                        principalTable: "InventoryCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryCountUnexpectedItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountUnexpectedItems_StockLocations_CurrentSystemS~",
                        column: x => x.CurrentSystemStockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountUnexpectedItems_StockLocations_ObservedStockL~",
                        column: x => x.ObservedStockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_ExpectedBranchId",
                table: "InventoryCountLines",
                column: "ExpectedBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_ExpectedStockLocationId",
                table: "InventoryCountLines",
                column: "ExpectedStockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_InventoryCountId_BarcodeSnapshot",
                table: "InventoryCountLines",
                columns: new[] { "InventoryCountId", "BarcodeSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_InventoryCountId_InventoryItemId",
                table: "InventoryCountLines",
                columns: new[] { "InventoryCountId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_InventoryCountId_IsCounted",
                table: "InventoryCountLines",
                columns: new[] { "InventoryCountId", "IsCounted" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_InventoryCountId_TagNumberSnapshot",
                table: "InventoryCountLines",
                columns: new[] { "InventoryCountId", "TagNumberSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_InventoryItemId",
                table: "InventoryCountLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_BranchId_StockLocationId",
                table: "InventoryCounts",
                columns: new[] { "BranchId", "StockLocationId" },
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_StartedAt",
                table: "InventoryCounts",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_Status",
                table: "InventoryCounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_StockLocationId",
                table: "InventoryCounts",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_CurrentSystemStockLocationId",
                table: "InventoryCountUnexpectedItems",
                column: "CurrentSystemStockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_InventoryCountId_BarcodeSnaps~",
                table: "InventoryCountUnexpectedItems",
                columns: new[] { "InventoryCountId", "BarcodeSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_InventoryCountId_InventoryIte~",
                table: "InventoryCountUnexpectedItems",
                columns: new[] { "InventoryCountId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_InventoryCountId_TagNumberSna~",
                table: "InventoryCountUnexpectedItems",
                columns: new[] { "InventoryCountId", "TagNumberSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_InventoryItemId",
                table: "InventoryCountUnexpectedItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_ObservedBranchId",
                table: "InventoryCountUnexpectedItems",
                column: "ObservedBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountUnexpectedItems_ObservedStockLocationId",
                table: "InventoryCountUnexpectedItems",
                column: "ObservedStockLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryCountLines");

            migrationBuilder.DropTable(
                name: "InventoryCountUnexpectedItems");

            migrationBuilder.DropTable(
                name: "InventoryCounts");
        }
    }
}
