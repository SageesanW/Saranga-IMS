using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashLedgerHardeningAndDatabaseTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Case-insensitive drawer code uniqueness per branch
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CashDrawers_BranchId_LowerDrawerCode""
                ON ""CashDrawers"" (""BranchId"", lower(""DrawerCode""))
                WHERE NOT ""IsDeleted"";
            ");

            // Immutability trigger for CashLedgerEntries (no updates, no deletes)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_prevent_cash_ledger_entries_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF current_setting('saranga.bypass_immutability_for_test', true) = 'true' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END IF;
                    RAISE EXCEPTION 'CashLedgerEntries are immutable: updates and deletions are strictly forbidden at the database level.';
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_cash_ledger_entries_no_mutation ON ""CashLedgerEntries"";
                CREATE TRIGGER trg_cash_ledger_entries_no_mutation
                BEFORE UPDATE OR DELETE ON ""CashLedgerEntries""
                FOR EACH ROW EXECUTE FUNCTION trg_prevent_cash_ledger_entries_mutation();
            ");

            // Immutability trigger for CashLedgerTransactions (no deletes, only lifecycle status updates)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_prevent_cash_ledger_transactions_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF current_setting('saranga.bypass_immutability_for_test', true) = 'true' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'CashLedgerTransactions are immutable: deletions are strictly forbidden at the database level.';
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF NEW.""Id"" <> OLD.""Id"" OR
                           NEW.""TransactionReference"" <> OLD.""TransactionReference"" OR
                           NEW.""BranchId"" <> OLD.""BranchId"" OR
                           NEW.""TransactionType"" <> OLD.""TransactionType"" OR
                           NEW.""SourceType"" <> OLD.""SourceType"" OR
                           (NEW.""SourceReferenceId"" IS DISTINCT FROM OLD.""SourceReferenceId"") OR
                           NEW.""Amount"" <> OLD.""Amount"" OR
                           NEW.""CurrencyCode"" <> OLD.""CurrencyCode"" OR
                           (NEW.""CategoryId"" IS DISTINCT FROM OLD.""CategoryId"") OR
                           NEW.""CreatedAt"" <> OLD.""CreatedAt"" OR
                           NEW.""CreatedBy"" <> OLD.""CreatedBy"" THEN
                            RAISE EXCEPTION 'Core financial fields of CashLedgerTransactions are immutable at the database level.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_cash_ledger_transactions_no_mutation ON ""CashLedgerTransactions"";
                CREATE TRIGGER trg_cash_ledger_transactions_no_mutation
                BEFORE UPDATE OR DELETE ON ""CashLedgerTransactions""
                FOR EACH ROW EXECUTE FUNCTION trg_prevent_cash_ledger_transactions_mutation();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_cash_ledger_transactions_no_mutation ON ""CashLedgerTransactions"";
                DROP FUNCTION IF EXISTS trg_prevent_cash_ledger_transactions_mutation();
                DROP TRIGGER IF EXISTS trg_cash_ledger_entries_no_mutation ON ""CashLedgerEntries"";
                DROP FUNCTION IF EXISTS trg_prevent_cash_ledger_entries_mutation();
                DROP INDEX IF EXISTS ""IX_CashDrawers_BranchId_LowerDrawerCode"";
            ");
        }
    }
}
