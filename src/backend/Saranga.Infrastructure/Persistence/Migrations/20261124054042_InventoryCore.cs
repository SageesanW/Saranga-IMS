using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "InventoryTagSequence",
                startValue: 100001L);

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TagNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossWeight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StoneWeight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetWeight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PieceCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MakingChargeMethod = table.Column<int>(type: "integer", nullable: false),
                    MakingChargeValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0.00m),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Purities_PurityId",
                        column: x => x.PurityId,
                        principalTable: "Purities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItemMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItemMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItemMedia_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItemSpecifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomValue = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItemSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItemSpecifications_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryItemSpecifications_SpecificationOptions_Specificat~",
                        column: x => x.SpecificationOptionId,
                        principalTable: "SpecificationOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItemSpecifications_Specifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "Specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemMedia_InventoryItemId_SortOrder",
                table: "InventoryItemMedia",
                columns: new[] { "InventoryItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Barcode",
                table: "InventoryItems",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_BranchId_Status",
                table: "InventoryItems",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_BranchId_StockLocationId",
                table: "InventoryItems",
                columns: new[] { "BranchId", "StockLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CategoryId_MaterialId_PurityId",
                table: "InventoryItems",
                columns: new[] { "CategoryId", "MaterialId", "PurityId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_MaterialId",
                table: "InventoryItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PurityId",
                table: "InventoryItems",
                column: "PurityId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_StockLocationId",
                table: "InventoryItems",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TagNumber",
                table: "InventoryItems",
                column: "TagNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemSpecifications_InventoryItemId_SpecificationId",
                table: "InventoryItemSpecifications",
                columns: new[] { "InventoryItemId", "SpecificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemSpecifications_SpecificationId",
                table: "InventoryItemSpecifications",
                column: "SpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemSpecifications_SpecificationOptionId",
                table: "InventoryItemSpecifications",
                column: "SpecificationOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItemMedia");

            migrationBuilder.DropTable(
                name: "InventoryItemSpecifications");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropSequence(
                name: "InventoryTagSequence");
        }
    }
}
