using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashLedgerAndDrawerMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CashLedgerSequence");

            migrationBuilder.CreateTable(
                name: "CashMovementCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AllowedDirection = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovementCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovementCategories_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashMovementCategories_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashLedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "LKR"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TransactionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgerTransactions", x => x.Id);
                    table.CheckConstraint("CK_CashLedgerTransactions_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CashLedgerTransactions_Status", "\"Status\" IN (1, 2)");
                    table.CheckConstraint("CK_CashLedgerTransactions_Type", "\"TransactionType\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_CashLedgerTransactions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerTransactions_CashLedgerTransactions_ReversalOfTra~",
                        column: x => x.ReversalOfTransactionId,
                        principalTable: "CashLedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerTransactions_CashMovementCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CashMovementCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerTransactions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerTransactions_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashDrawerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "LKR"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_CashLedgerEntries_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CashLedgerEntries_Direction", "\"Direction\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_CashDrawers_CashDrawerId",
                        column: x => x.CashDrawerId,
                        principalTable: "CashDrawers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_CashLedgerTransactions_CashLedgerTransact~",
                        column: x => x.CashLedgerTransactionId,
                        principalTable: "CashLedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_BranchId_CreatedAt",
                table: "CashLedgerEntries",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_CashDrawerId",
                table: "CashLedgerEntries",
                column: "CashDrawerId");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_CashLedgerTransactionId",
                table: "CashLedgerEntries",
                column: "CashLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_CreatedBy",
                table: "CashLedgerEntries",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_ShiftId",
                table: "CashLedgerEntries",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_BranchId_TransactionAt",
                table: "CashLedgerTransactions",
                columns: new[] { "BranchId", "TransactionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_CategoryId",
                table: "CashLedgerTransactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_CreatedAt",
                table: "CashLedgerTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_CreatedBy",
                table: "CashLedgerTransactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_ReversalOfTransactionId",
                table: "CashLedgerTransactions",
                column: "ReversalOfTransactionId",
                unique: true,
                filter: "\"ReversalOfTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_SourceType_SourceReferenceId",
                table: "CashLedgerTransactions",
                columns: new[] { "SourceType", "SourceReferenceId" },
                unique: true,
                filter: "\"SourceReferenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_Status",
                table: "CashLedgerTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_TransactionReference",
                table: "CashLedgerTransactions",
                column: "TransactionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerTransactions_UpdatedBy",
                table: "CashLedgerTransactions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementCategories_Code",
                table: "CashMovementCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementCategories_CreatedBy",
                table: "CashMovementCategories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementCategories_IsActive",
                table: "CashMovementCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementCategories_UpdatedBy",
                table: "CashMovementCategories",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashLedgerEntries");

            migrationBuilder.DropTable(
                name: "CashLedgerTransactions");

            migrationBuilder.DropTable(
                name: "CashMovementCategories");

            migrationBuilder.DropSequence(
                name: "CashLedgerSequence");
        }
    }
}
